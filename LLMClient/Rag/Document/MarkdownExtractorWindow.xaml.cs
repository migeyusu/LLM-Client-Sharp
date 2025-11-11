using System.Windows;
using System.Windows.Input;

namespace LLMClient.Rag.Document;

public partial class MarkdownExtractorWindow : Window
{
    public MarkdownExtractorWindow()
    {
        InitializeComponent();
    }

    MarkdownExtractorViewModel viewModel => (DataContext as MarkdownExtractorViewModel)!;

    private void OK_OnClick(object sender, RoutedEventArgs e)
    {
        this.DialogResult = true;
        this.Close();
    }

    private void RefreshCommandBinding_OnExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        if (e.Parameter is MarkdownNode node)
        {
            viewModel.GenerateSummary(node);
        }
    }
}