using System.Windows;
using System.Windows.Input;
using LLMClient.Data;
using LLMClient.UI;
using LLMClient.UI.Log;
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
                var summarySize = Extension.SummarySize;
                try
                {
                    Logs.Start();
                    IsSummaryFailed = false;
                    IsProcessing = true;
                    await PromptsCache.LoadAsync(digestClient.Endpoint.Name, digestClient.Model.Id, summarySize);
                    /*Func<string, CancellationToken, Task<string>> summaryDelegate = async (s, cancellationToken) =>
                    {
                        await Task.Delay(1000, cancellationToken);
                        var length = s.Length;
                        return s.Substring(0, int.Min(length, 1000));
                    };*/
                    var summaryDelegate =
                        digestClient.CreateSummaryDelegate(semaphoreSlim, SummaryLanguageIndex,
                            ContextGenerator(SummaryLanguageIndex), PromptsCache,
                            logger: this.Logs, summarySize: summarySize, retryCount: 3);

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
                var summarySize = Extension.SummarySize;
                var summaryDelegate =
                    digestClient.CreateSummaryDelegate(semaphoreSlim, SummaryLanguageIndex,
                        ContextGenerator(SummaryLanguageIndex), PromptsCache.NoCache,
                        logger: this.Logs, summarySize: summarySize, retryCount: 3);
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
}