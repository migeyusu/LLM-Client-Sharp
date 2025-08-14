using System.Collections.Specialized;
using System.IO;
using System.Text;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.TextFormatting;
using Google.Apis.Util;
using LLMClient.Abstraction;
using LLMClient.Dialog;
using LLMClient.Endpoints;
using LLMClient.UI;
using LLMClient.UI.Component;
using LLMClient.UI.Log;
using Microsoft.Extensions.Logging;
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
            if (_status == ConstructStatus.Constructing)
            {
                ConstructionLogs.Stop();
            }

            _status = value;
            if (value == ConstructStatus.Constructing)
            {
                ConstructionLogs.Start();
            }

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

    [JsonIgnore] public LogsViewModel ConstructionLogs { get; set; } = new LogsViewModel();

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

    /*public ICommand ClearEmbeddingCommand => new ActionCommand(async o =>
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
        }
    });*/

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

    public long SummaryTokensConsumption { get; set; } = 0;

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


    private const int SummaryTrigger = 2000; // 摘要触发长度（中文字符数）

    protected Func<string, CancellationToken, Task<string>> CreateLLMCall(ILLMChatClient client, int summarySize = 500,
        int retryCount = 3, ILogger? logger = null)
    {
        return async (content, token) =>
        {
            if (string.IsNullOrEmpty(content))
            {
                logger?.LogInformation("内容为空，不进行摘要。");
                return string.Empty;
            }

            if (content.Length < SummaryTrigger)
            {
                logger?.LogInformation("内容长度未超过{SummaryTrigger}，不进行摘要。", SummaryTrigger);
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
            int tryCount = 0;
            var response = new CompletedResult();
            while (tryCount < retryCount)
            {
                response = await client.SendRequest(dialogContext, token);
                tryCount++;
                SummaryTokensConsumption += response.Usage?.TotalTokenCount ?? 0;
                var textResponse = response.TextResponse;
                if (!string.IsNullOrEmpty(textResponse) && !response.IsInterrupt)
                {
                    return textResponse;
                }
            }

            throw new InvalidOperationException("LLM response failed after " + retryCount + " attempts. error: " +
                                                response.ErrorMessage);
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