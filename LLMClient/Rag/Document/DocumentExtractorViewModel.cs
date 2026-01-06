using System.Text;
using System.Windows;
using System.Windows.Input;
using LLMClient.Abstraction;
using LLMClient.Agent;
using LLMClient.Component.ViewModel.Base;
using LLMClient.Data;
using LLMClient.Endpoints;
using LLMClient.Log;
using Microsoft.Extensions.Logging;
using Microsoft.Xaml.Behaviors.Core;

namespace LLMClient.Rag.Document;

public abstract class DocumentExtractorViewModel<T, TK> : BaseViewModel where T : RawNode<T, TK> where TK : IContentUnit
{
    public string Title
    {
        get => _title;
        set
        {
            if (value == _title) return;
            _title = value;
            OnPropertyChanged();
        }
    }

    public abstract int CurrentStep { get; set; }

    private int _summaryLanguageIndex;

    /// <summary>
    /// 0:english, 1:chinese
    /// </summary>
    public int SummaryLanguageIndex
    {
        get => _summaryLanguageIndex;
        set
        {
            if (value == _summaryLanguageIndex) return;
            _summaryLanguageIndex = value;
            OnPropertyChanged();
        }
    }


    private bool _isProcessing;

    public bool IsProcessing
    {
        get => _isProcessing;
        set
        {
            if (value == _isProcessing) return;
            _isProcessing = value;
            OnPropertyChanged();
        }
    }

    private CancellationTokenSource? _processTokenSource;

    public bool CanCancel
    {
        get => _canCancel;
        set
        {
            if (value == _canCancel) return;
            _canCancel = value;
            OnPropertyChanged();
        }
    }

    public ICommand CancelCommand => new ActionCommand((o => { _processTokenSource?.Cancel(); }));

    public LogsViewModel Logs { get; set; } = new LogsViewModel();

    private IList<T> _contentNodes = Array.Empty<T>();

    public IList<T> ContentNodes
    {
        get => _contentNodes;
        set
        {
            if (Equals(value, _contentNodes)) return;
            _contentNodes = value;
            OnPropertyChanged();
        }
    }

    private double _progressValue;

    public double ProgressValue
    {
        get => _progressValue;
        set
        {
            if (value.Equals(_progressValue)) return;
            _progressValue = value;
            OnPropertyChanged();
        }
    }

    private bool _isSummaryFailed;

    public bool IsSummaryFailed
    {
        get => _isSummaryFailed;
        set
        {
            if (value == _isSummaryFailed) return;
            _isSummaryFailed = value;
            OnPropertyChanged();
        }
    }

    protected abstract Func<T, string> ContextGenerator(int languageIndex);

    protected readonly PromptsCache PromptsCache;

    private readonly RagOption _ragOption;

    private string _title = string.Empty;

    private bool _canCancel;

    protected DocumentExtractorViewModel(RagOption ragOption, PromptsCache promptsCache)
    {
        _ragOption = ragOption;
        PromptsCache = promptsCache;
    }

    public ICommand ClearCacheCommand => new ActionCommand((o) =>
    {
        if (MessageBox.Show("是否清除缓存？", "确认", MessageBoxButton.OKCancel, MessageBoxImage.Question)
            == MessageBoxResult.OK)
        {
            this.PromptsCache.Clear();
        }
    });

    protected async void GenerateSummary()
    {
        var digestClient = _ragOption.DigestClient;
        if (digestClient == null)
        {
            throw new InvalidOperationException("Digest client is not set.");
        }

        var nodeCount = 0;
        foreach (var contentNode in this.ContentNodes)
        {
            nodeCount += contentNode.CountRecursive();
        }

        var progressCount = 0;
        var progress = new Progress<T>(node =>
        {
            progressCount++;
            this.ProgressValue = (double)progressCount / nodeCount;
            // 会自动在UI线程调用
            Logs.LogInformation("Processing node {0}, level: {1}",
                node.Title, node.Level);
        });
        using (_processTokenSource = new CancellationTokenSource())
        {
            var digestParallelism = _ragOption.MaxDigestParallelism <= 0 ? 5 : _ragOption.MaxDigestParallelism;
            using (var semaphoreSlim = new SemaphoreSlim(digestParallelism, digestParallelism))
            {
                try
                {
                    Logs.Start();
                    IsSummaryFailed = false;
                    IsProcessing = true;
                    await PromptsCache.LoadAsync(digestClient.Endpoint.Name, digestClient.Model.Id, SummarySize);
                    /*Func<string, CancellationToken, Task<string>> summaryDelegate = async (s, cancellationToken) =>
                    {
                        await Task.Delay(1000, cancellationToken);
                        var length = s.Length;
                        return s.Substring(0, int.Min(length, 1000));
                    };*/
                    var summaryDelegate =
                        CreateSummaryDelegate(digestClient, semaphoreSlim, SummaryLanguageIndex,
                            ContextGenerator(SummaryLanguageIndex), PromptsCache,
                            logger: this.Logs, summarySize: SummarySize, retryCount: 3);

                    CanCancel = true;
                    await Parallel.ForEachAsync(this.ContentNodes,
                        new ParallelOptions() { CancellationToken = _processTokenSource.Token },
                        async (node, token) =>
                        {
                            try
                            {
                                await node.GenerateSummarize<T, TK>(summaryDelegate, this.Logs,
                                    progress, token: token);
                            }
                            catch (OperationCanceledException)
                            {
                                //不作处理
                            }
                            catch (Exception)
                            {
                                if (!_processTokenSource.IsCancellationRequested)
                                {
                                    this.Logs.LogWarning("由于一个摘要任务失败，所有任务已被取消");
                                    await _processTokenSource.CancelAsync();
                                }

                                throw;
                            }
                        });
                    MessageBox.Show("Summary generated successfully!");
                }
                catch (Exception e)
                {
                    IsSummaryFailed = true;
                    // await promptsCache.SaveAsync();
                    MessageBox.Show($"Failed to generate summary: {e.Message}");
                }
                finally
                {
                    CanCancel = false;
                    IsProcessing = false;
                    Logs.Stop();
                }
            }
        }
    }

    public async void GenerateSummary(T node)
    {
        try
        {
            IsProcessing = true;
            var digestClient = _ragOption.DigestClient;
            if (digestClient == null)
            {
                throw new InvalidOperationException("Digest client is not set.");
            }

            using (var semaphoreSlim = new SemaphoreSlim(1, 1))
            {
                var summaryDelegate =
                    CreateSummaryDelegate(digestClient, semaphoreSlim, SummaryLanguageIndex,
                        ContextGenerator(SummaryLanguageIndex), PromptsCache.NoCache,
                        logger: this.Logs, summarySize: SummarySize, retryCount: 3);
                using (var source = new CancellationTokenSource(TimeSpan.FromSeconds(60)))
                {
                    var summaryRaw = node.GetSummaryRaw();
                    node.Summary = await summaryDelegate(node, source.Token);
                    PromptsCache?.AddOrUpdate(summaryRaw, node.Summary);
                }
            }
        }
        catch (Exception exception)
        {
            MessageBox.Show(exception.Message);
        }
        finally
        {
            IsProcessing = false;
        }
    }

    public const int SummarySize = 1024; // 摘要长度

    private const int SummaryTrigger = 3072; // 摘要触发长度

    /// <summary>
    /// 
    /// </summary>
    /// <param name="client"></param>
    /// <param name="clientSemaphore"></param>
    /// <param name="language">0: English, 1: Chinese</param>
    /// <param name="contextGenerator"></param>
    /// <param name="cache"></param>
    /// <param name="logger"></param>
    /// <param name="summarySize"></param>
    /// <param name="retryCount"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public static Func<T, CancellationToken, Task<string>> CreateSummaryDelegate(ILLMChatClient client,
        SemaphoreSlim clientSemaphore, int language, Func<T, string> contextGenerator,
        PromptsCache cache, ILogger? logger = null, int summarySize = SummarySize, int retryCount = 3)
    {
        var modelParams = client.Parameters;
        modelParams.Streaming = false;
        if (client.Model.MaxTokensEnable)
        {
            modelParams.MaxTokens =
                int.Min(summarySize * 6, client.Model.MaxTokenLimit); // 设置最大令牌数至少为摘要长度的6倍（以包括reasoning）
        }

        return async (node, token) =>
        {
            var context = contextGenerator(node);
            var raw = node.GetSummaryRaw();
            if (string.IsNullOrEmpty(raw))
            {
                logger?.LogInformation("内容为空，不进行摘要。");
                return string.Empty;
            }

            if (raw.Length < SummaryTrigger)
            {
                logger?.LogInformation("内容长度未超过{SummaryTrigger}，不进行摘要。", SummaryTrigger);
                return raw;
            }

            if (cache.TryGetValue(raw, out var result))
            {
                return result;
            }

            var response = new CompletedResult();
            await clientSemaphore.WaitAsync(token);
            try
            {
                //经过测试，使用 ‘Use the same language of the text blocks’ 时，gpt5 nano依然会错乱，对英语文档生成日语总结，所以手动限制语言
                var stringBuilder = new StringBuilder();
                if (language == 0) // 英语
                {
                    stringBuilder.Append(
                        $"Provide a concise and complete summarization of the following text blocks that does not exceed {summarySize} words. " +
                        $"\n{contextGenerator(node)}" +
                        "\nThis summary must always:" +
                        "\n- Use English" +
                        "\n- Focus on the most significant aspects of the text blocks" +
                        "\n- Include details from any existing summary" +
                        "\nThis summary must never:" +
                        "\n- Critique, correct, interpret, presume, or assume" +
                        "\n- Identify faults, mistakes, misunderstanding, or correctness" +
                        "\n- Analyze what has not occurred" +
                        "\n- Exclude details from any existing summary" +
                        "\n\nPlease summarize the following text blocks until end:\n\n");
                }
                else // 中文
                {
                    stringBuilder.Append(
                        $"请对以下文本块进行简洁而完整的总结，不超过 {summarySize} 字。" +
                        $"\n{context}" +
                        "\n该摘要必须始终：" +
                        "\n- 使用中文" +
                        "\n- 关注文本块最重要的方面" +
                        "\n- 包含任何现有摘要中的详细信息" +
                        "\n该摘要绝不能：" +
                        "\n- 批评、纠正、解释、推测或假设" +
                        "\n- 指出错误、失误、误解或正确性" +
                        "\n- 分析未发生的事情" +
                        "\n- 排除任何现有摘要中的详细信息" +
                        "\n\n请总结以下文本块直到结束：\n\n");
                }

                stringBuilder.Append(raw);
                var promptAgent = new PromptBasedAgent(client, null);
                var textResponse =
                    await promptAgent.GetMessageAsync(stringBuilder.ToString(), cancellationToken: token);
                if (!string.IsNullOrEmpty(textResponse) && !response.IsInterrupt)
                {
                    cache.TryAdd(raw, textResponse);
                    return textResponse;
                }
            }
            catch (Exception ex)
            {
                logger?.LogError("Summary error: {ErrorMessage}", ex.Message);
                throw;
            }
            finally
            {
                clientSemaphore.Release();
            }

            throw new InvalidOperationException("LLM response failed after " + retryCount + " attempts. error: " +
                                                response.ErrorMessage);
        };
    }
}