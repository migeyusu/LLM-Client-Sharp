using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using LLMClient.Component.Render;

namespace LLMClient.Test;

public partial class TestWindow : Window, INotifyPropertyChanged
{
    public TestWindow()
    {
        this.DataContext = this;
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
    }

    public int FormulaHasErrors { get; set; }

    public string Formula
    {
        get => _formula;
        set
        {
            if (value == _formula) return;
            _formula = value;
            OnPropertyChanged();
        }
    }

    public ObservableCollection<Exception> Exceptions { get; set; } = new();

    public ImageSource? LatexSource
    {
        get => _latexSource;
        set
        {
            if (Equals(value, _latexSource)) return;
            _latexSource = value;
            OnPropertyChanged();
        }
    }

    public FlowDocument Document { get; set; } = new FlowDocument();

    public ObservableCollection<string> Collection { get; set; } = new ObservableCollection<string>();

    public CancellationTokenSource? RequestTokenSource { get; set; }

    private MathJaxLatexRenderService? _latexRenderService = null;

    private ImageSource? _latexSource;
    private string _formula = string.Empty;

    public void Stop()
    {
        RequestTokenSource?.Cancel();
    }

    public async void Start()
    {
        if (_latexRenderService == null)
        {
            _latexRenderService = await MathJaxLatexRenderService.CreateAsync(WebView2);
        }

        var imageSource =
            await _latexRenderService.RenderAsync(
                "R(f)(\\theta, s) = \\iint_{-\\infty}^{\\infty} f(x, y) \\, \\delta(x \\cos \\theta + y \\sin \\theta - s) \\, dx \\, dy");
        this.LatexSource = imageSource;

        RequestTokenSource?.Cancel();
        RequestTokenSource = new CancellationTokenSource();
        try
        {
            using (var cancellationTokenSource = RequestTokenSource)
            {
                var cancellationToken = cancellationTokenSource.Token;
                using (var blockingCollection = new BlockingCollection<string>())
                {
                    var customRenderer = CustomRenderer.NewRenderer(Document);
                    var task = Task.Run(() =>
                    {
                        RendererExtensions.StreamParse(blockingCollection,
                            (processer, block) =>
                            {
                                // customRenderer.RenderItem();
                                this.Dispatcher.Invoke(() =>
                                {
                                    Collection.Clear();
                                    customRenderer.AppendMarkdownObject(block);
                                });
                            });
                    });
                    await Task.Run((async () =>
                    {
                        var fakeFilePath = @"E:\Dev\Markdig\src\Markdig.Tests\Specs\AbbreviationSpecs.md";
                        var fakeResponse = await File.ReadAllTextAsync(fakeFilePath, cancellationToken);
                        int index = 0;
                        while (index < fakeResponse.Length)
                        {
                            if (cancellationToken.IsCancellationRequested)
                            {
                                break;
                            }

                            int next = Random.Shared.Next(8);
                            var chunk = fakeResponse.Substring(index, Math.Min(next, fakeResponse.Length - index));
                            blockingCollection.Add(chunk, cancellationToken);
                            Dispatcher.Invoke(() => Collection.Add(chunk));
                            index += next;
                            await Task.Delay(100, cancellationToken);
                        }
                    }), cancellationToken);
                    blockingCollection.CompleteAdding();
                    await task.WaitAsync(cancellationToken);
                }
            }
        }
        catch (Exception e)
        {
            MessageBox.Show(e.Message);
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void Cancel_OnClick(object sender, RoutedEventArgs e)
    {
        this.Stop();
    }

    private void OK_OnClick(object sender, RoutedEventArgs e)
    {
        this.Start();
    }

    private void Input_OnClick(object sender, RoutedEventArgs e)
    {
        this.Formula = FormulaInput.Text;
    }
}