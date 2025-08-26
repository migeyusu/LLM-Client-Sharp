using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using LLMClient.Data;
using LLMClient.UI.Component;
using LLMClient.UI.Log;
using Microsoft.Extensions.Logging;

namespace LLMClient.Rag.Document;

public partial class MarkdownExtractorWindow : Window, INotifyPropertyChanged
{
    private int _currentStep = 0;

    public int CurrentStep
    {
        get => _currentStep;
        set
        {
            if (value == _currentStep) return;
            _currentStep = value;
            OnPropertyChanged();
            switch (value)
            {
                // 根据步骤执行不同的操作
                case 0:
                    this.Title = "Markdown Extractor - Step 1: Analyze Content";
                    break;
                case 1:
                    this.Title = "Markdown Extractor - Step 2: Generate Summary";
                    GenerateSummary();
                    break;
            }
        }
    }

    public LogsViewModel Logs { get; set; } = new LogsViewModel();

    private PromptsCache? _promptsCache;

    private readonly RagOption _ragOption;

    private async void GenerateSummary()
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
        var progress = new Progress<MarkdownNode>(node =>
        {
            progressCount++;
            this.ProgressValue = (double)progressCount / nodeCount;
            // 会自动在UI线程调用
            Logs.LogInformation("Processing node {0}, level: {1}",
                node.Title, node.Level);
        });
        using (var semaphoreSlim = new SemaphoreSlim(5, 5))
        {
            var summarySize = Extension.SummarySize;
            _promptsCache ??= new PromptsCache(Guid.NewGuid().ToString(), PromptsCache.CacheFolderPath,
                digestClient.Endpoint.Name, digestClient.Model.Id) { OutputSize = summarySize };

            try
            {
                Logs.Start();
                IsProcessing = true;
                // await promptsCache.InitializeAsync();
                Func<string, CancellationToken, Task<string>> promptFake = async (s, cancellationToken) =>
                {
                    await Task.Delay(1000, cancellationToken);
                    var length = s.Length;
                    return s.Substring(0, int.Min(length, 1000));
                };
                var summaryDelegate =
                    digestClient.CreateSummaryDelegate(semaphoreSlim, SummaryLanguageIndex, _promptsCache,
                        logger: this.Logs,
                        summarySize: summarySize, retryCount: 3);
                await Parallel.ForEachAsync(this.ContentNodes, new ParallelOptions(),
                    async (node, token) =>
                    {
                        await node.GenerateSummarize<MarkdownNode, MarkdownText>(promptFake, this.Logs,
                            progress, token: token);
                    });
                MessageEventBus.Publish("Summary generated successfully!");
            }
            catch (Exception e)
            {
                // await promptsCache.SaveAsync();
                MessageBox.Show($"Failed to generate summary: {e.Message}");
            }
            finally
            {
                IsProcessing = false;
                Logs.Stop();
            }
        }
    }

    private int _summaryLanguageIndex;

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

    private IList<MarkdownNode> _contentNodes = Array.Empty<MarkdownNode>();

    public IList<MarkdownNode> ContentNodes
    {
        get => _contentNodes;
        set
        {
            if (Equals(value, _contentNodes)) return;
            _contentNodes = value;
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

    private readonly MarkdownParser _parser = new MarkdownParser();

    public MarkdownExtractorWindow(string markdownPath, RagOption ragOption)
    {
        this.DataContext = this;
        _ragOption = ragOption;
        AnalyzeNode(markdownPath);
        InitializeComponent();
    }

    private async void AnalyzeNode(string markdownPath)
    {
        try
        {
            ContentNodes = await _parser.Parse(markdownPath);
        }
        catch (Exception e)
        {
            MessageBox.Show(e.Message);
        }
    }

    private void OK_OnClick(object sender, RoutedEventArgs e)
    {
        this.DialogResult = true;
        this.Close();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}