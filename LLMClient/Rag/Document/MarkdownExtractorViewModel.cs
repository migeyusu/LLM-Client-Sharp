using System.Windows;
using LLMClient.Data;

namespace LLMClient.Rag.Document;

public class MarkdownExtractorViewModel : DocumentExtractorViewModel<MarkdownNode, MarkdownText>
{
    private int _currentStep = 0;

    public override int CurrentStep
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

    protected override Func<MarkdownNode, string> ContextGenerator(int languageIndex)
    {
        return (MarkdownNode markdownNode) =>
        {
            var title = markdownNode.Title.Trim();
            string context;
            switch (languageIndex)
            {
                case 0:
                    if (markdownNode.HasChildren)
                    {
                        context =
                            $"The text blocks are hierarchical summaries or content under the heading '{title}' in a markdown document.";
                    }
                    else
                    {
                        context =
                            $"The text blocks are the original content of heading '{title}' in a pdf document." +
                            $"The content is provided in its original Markdown format, which may include headings, paragraphs, lists, code blocks, and other elements.";
                    }

                    break;
                case 1:
                    if (markdownNode.HasChildren)
                    {
                        context =
                            $"这些文本块是Markdown文档中标题'{title}'下的摘要或内容的组合。";
                    }
                    else
                    {
                        context =
                            $"这些文本块是Markdown文档中标题'{title}'的原始内容。" +
                            $"这些内容以原始Markdown格式提供，可能包含标题、段落、列表、代码块等元素。";
                    }

                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            return context;
        };
    }

    private readonly MarkdownParser _parser = new();

    public MarkdownExtractorViewModel(string markdownPath, RagOption ragOption, PromptsCache promptsCache) : base(
        ragOption, promptsCache)
    {
        AnalyzeNode(markdownPath);
        this.Title = "Markdown Extractor - Step 1: Analyze Content";
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
}