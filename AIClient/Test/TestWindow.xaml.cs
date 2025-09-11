using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Documents;
using LLMClient.UI.Render;

namespace LLMClient.Test;

public partial class TestWindow : Window, INotifyPropertyChanged
{
    public TestWindow()
    {
        this.DataContext = this;
        InitializeComponent();
    }

    public FlowDocument Document { get; set; } = new FlowDocument();

    public ObservableCollection<string> Collection { get; set; } = new ObservableCollection<string>();

    public CancellationTokenSource? RequestTokenSource { get; set; }

    public void Stop()
    {
        RequestTokenSource?.Cancel();
    }

    public async void Start()
    {
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
}