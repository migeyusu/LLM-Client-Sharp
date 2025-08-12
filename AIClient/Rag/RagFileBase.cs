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
using LLMClient.UI.Component;
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

    public bool IsInitialized
    {
        get => _isInitialized;
        protected set
        {
            if (value == _isInitialized) return;
            _isInitialized = value;
            OnPropertyChanged();
        }
    }

    public ICommand ClearCommand => new ActionCommand((async o =>
    {
        if (Status == ConstructStatus.Constructing)
        {
            return;
        }

        if (MessageBox.Show("是否要清空数据？", "提示",
                MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No,
                MessageBoxOptions.DefaultDesktopOnly) != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            await DeleteAsync();
            MessageEventBus.Publish("数据已清空");
        }
        catch (Exception e)
        {
            MessageBox.Show($"删除数据失败: {e.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }
    }));

    public virtual Task InitializeAsync()
    {
        if (IsInitialized)
        {
            return Task.CompletedTask;
        }

        if (this.Status == ConstructStatus.Constructing)
        {
            //说明上次构建还未完成，需要删除之前的脏数据重新构建。
            this.Status = ConstructStatus.NotConstructed;
            return this.DeleteAsync();
        }

        IsInitialized = true;
        return Task.CompletedTask;
    }

    private CancellationTokenSource? _constructionCancellationTokenSource;
    private Task? _constructionTask;
    private bool _isInitialized;

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

        try
        {
            ErrorMessage = null;
            //must ensure the file is deleted before constructing again.
            await this.DeleteAsync(cancellationToken);
            // await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
            Status = ConstructStatus.Constructing;
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

    protected Func<string, CancellationToken, Task<string>> CreateLLMCall(ILLMChatClient client, int summarySize = 100)
    {
        return async (content, token) =>
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
            var response = await client.SendRequest(dialogContext, token);
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