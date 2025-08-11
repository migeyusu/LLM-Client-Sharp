using System.IO;
using System.Text;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.TextFormatting;
using Google.Apis.Util;
using LLMClient.Abstraction;
using LLMClient.Dialog;
using LLMClient.UI;
using Microsoft.Xaml.Behaviors.Core;

namespace LLMClient.Rag;

public abstract class RagFileBase : BaseViewModel, IRagFileSource
{
    private string _name = string.Empty;
    private string? _errorMessage;
    private ConstructStatus _status = ConstructStatus.NotConstructed;

    public string Name
    {
        get => _name;
        set
        {
            if (value == _name) return;
            _name = value;
            OnPropertyChanged();
        }
    }

    public Guid Id { get; set; } = Guid.NewGuid();

    protected RagFileBase()
    {
    }

    protected RagFileBase(FileInfo fileInfo)
    {
        this.FilePath = fileInfo.FullName;
        this.FileSize = fileInfo.Length;
        this.EditTime = fileInfo.LastWriteTime;
        this.Name = fileInfo.Name;
    }

    public string FilePath { get; set; } = string.Empty;
    public DateTime EditTime { get; set; }
    public long FileSize { get; set; } = 0;

    public ConstructStatus Status
    {
        get => _status;
        set
        {
            if (value == _status) return;
            _status = value;
            OnPropertyChanged();
        }
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        set
        {
            if (value == _errorMessage) return;
            _errorMessage = value;
            OnPropertyChanged();
        }
    }

    [JsonIgnore] public abstract DocumentFileType FileType { get; }

    [JsonIgnore]
    public virtual string DocumentId
    {
        get { return $"{FileType}_{Id}"; }
    }

    public virtual Task InitializeAsync()
    {
        if (this.Status == ConstructStatus.Constructing)
        {
            //说明上次构建还未完成，需要删除之前的脏数据重新构建。
            this.Status = ConstructStatus.NotConstructed;
            return this.DeleteAsync();
        }

        return Task.CompletedTask;
    }

    private CancellationTokenSource? _constructionCancellationTokenSource;
    private Task? _constructionTask;

    public ICommand SwitchConstructCommand => new ActionCommand(async o =>
    {
        if (Status == ConstructStatus.Constructing)
        {
            await StopConstruct();
        }
        else
        {
            if (Status == ConstructStatus.Constructed)
            {
                if (MessageBox.Show("是否要重新构建？", "提示",
                        MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No,
                        MessageBoxOptions.DefaultDesktopOnly) != MessageBoxResult.Yes)
                {
                    return;
                }
            }

            await this.DeleteAsync();
            _constructionCancellationTokenSource = new CancellationTokenSource();
            _constructionTask = ConstructAsync(_constructionCancellationTokenSource.Token);
            try
            {
                await _constructionTask;
            }
            finally
            {
                _constructionCancellationTokenSource?.Dispose();
                _constructionCancellationTokenSource = null;
            }
        }
    });

    public virtual async Task ConstructAsync(CancellationToken cancellationToken = default)
    {
        if (Status == ConstructStatus.Constructing)
        {
            // Already constructing, no need to construct again.
            return;
        }

        Status = ConstructStatus.Constructing;
        try
        {
            ErrorMessage = null;
            // await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
            await ConstructCore(cancellationToken);
            Status = ConstructStatus.Constructed;
        }
        catch (Exception e)
        {
            ErrorMessage = e.Message;
            Status = ConstructStatus.Error;
        }
    }

    public async Task StopConstruct()
    {
        if (_constructionCancellationTokenSource != null && _constructionTask != null)
        {
            await _constructionCancellationTokenSource.CancelAsync();
            try
            {
                await _constructionTask;
            }
            catch (OperationCanceledException)
            {
                // Expected, suppress to prevent unhandled exception.
            }
        }
    }

    public abstract Task DeleteAsync(CancellationToken cancellationToken = default);

    protected abstract Task ConstructCore(CancellationToken cancellationToken = default);

    public abstract Task<ISearchResult> QueryAsync(string query, dynamic options,
        CancellationToken cancellationToken = default);


    private const int SummaryTrigger = 1000; // 摘要触发长度

    protected Func<string, Task<string>> CreateLLMCall(ILLMChatClient client, int summarySize = 100)
    {
        return async (content) =>
        {
            if (string.IsNullOrEmpty(content))
            {
                return string.Empty;
            }

            if (content.Length < SummaryTrigger)
            {
                return content;
            }

            var stringBuilder = new StringBuilder("请为以下内容生成一个简短的摘要，要求：\r\n" +
                                                  "1. 摘要使用的语言和原文一致。\r\n" +
                                                  "2. 摘要长度不超过" + summarySize + "个字。\r\n" +
                                                  "3. 摘要内容应包含原文的主要信息。\r\n" +
                                                  "4. 摘要应尽量简洁明了。\r\n");
            stringBuilder.Append(content);
            var dialogContext = new DialogContext(new[]
            {
                new RequestViewItem() { TextMessage = stringBuilder.ToString(), }
            });
            var response = await client.SendRequest(dialogContext);
            var textResponse = response.TextResponse;
            if (string.IsNullOrEmpty(textResponse))
            {
                throw new InvalidOperationException("LLM response is empty.");
            }

            return textResponse;
        };
    }
}

public class SimpleQueryResult : ISearchResult
{
    public string? DocumentId { get; set; }

    public IList<string>? TextBlocks { get; set; }

    public SimpleQueryResult(string documentId, IList<string>? textBlocks)
    {
        DocumentId = documentId;
        TextBlocks = textBlocks;
    }
}