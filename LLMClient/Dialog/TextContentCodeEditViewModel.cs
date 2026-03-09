using System.Text;
using System.Windows.Controls;
using System.Windows.Documents;
using LLMClient.Component.Render;
using LLMClient.Component.Utility;
using Markdig.Renderers.Html;
using Microsoft.Extensions.AI;
using Microsoft.Win32;
using System.Windows;
using CommunityToolkit.Mvvm.Input;

namespace LLMClient.Dialog;

public class TextContentCodeEditViewModel : TextContentEditViewModel
{

    public Task<string?> GetPersistString()
    {
        return GetPrompt(true);
    }

    public async Task<string?> GetPrompt(bool includeFilePath = false)
    {
        var doc = await WaitAsyncProperty<FlowDocument>(nameof(EditDocument));
        if (!doc.Blocks.Any())
        {
            return null;
        }

        var sb = new StringBuilder();
        foreach (var block in doc.Blocks)
        {
            // 判断是否为代码块 UI 容器
            if (block is BlockUIContainer { Child: Expander { Content: EditableCodeViewModel codeVm } })
            {
                // 重构 Markdown 代码块
                sb.Append($"```{codeVm.Name}");
                if (includeFilePath && !string.IsNullOrEmpty(codeVm.FileLocation))
                {
                    var htmlAttributes = new HtmlAttributes();
                    htmlAttributes.AddProperty("fileLocation", codeVm.FileLocation);
                    var codeBlockAttributes = htmlAttributes.ToCodeBlockAttributes();
                    sb.Append($" {codeBlockAttributes}");
                }
                else
                {
                    sb.AppendLine();
                }

                sb.AppendLine(codeVm.Code);
                sb.AppendLine("```");
            }
            else if (block is Paragraph paragraph)
            {
                // 非代码块直接提取纯文本（因为在正向渲染时我们保留了原始 Markdown 符号）
                var textRange = new TextRange(paragraph.ContentStart, paragraph.ContentEnd);
                var text = textRange.Text.TrimEnd('\r', '\n'); // 清除多余的自动换行
                sb.AppendLine(text);
                sb.AppendLine();
            }
        }

        return sb.ToString().TrimEnd(); // 去掉末尾多余的空行
    }

    public FlowDocument EditDocument
    {
        get
        {
            //只初始化一次，后续通过绑定直接修改 FlowDocument 内容
            return GetAsyncProperty(async () =>
            {
                var textContentText = Content.Text;
                if (string.IsNullOrEmpty(textContentText))
                {
                    return Document;
                }

                var renderer = CustomMarkdownRenderer.EditRenderer(Document);
                await renderer.RenderMarkdown(textContentText);
                return Document;
            }, Document);
        }
    }

    private readonly string? _originalText;

    private readonly Lazy<FlowDocument> _docLazy = new(() => new FlowDocument());
    private FlowDocument Document => _docLazy.Value;

    public TextContentCodeEditViewModel(TextContent textContent, string? messageId) : base(textContent, messageId)
    {
        this._originalText = textContent.Text;
        AddCodeFileCommand = new RelayCommand<object>(AddCodeFile);
    }
    
    private async void AddCodeFile(object? o)
    {
        RichTextBox? richTextBox = null;
        if (o is RichTextBox rtb)
        {
            richTextBox = rtb;
        }

        richTextBox ??= ((DependencyObject?)o)?.FindVisualChild<RichTextBox>();

        if (richTextBox == null)
        {
            return;
        }

        var openFileDialog = new OpenFileDialog()
        {
            Filter = "Text files (*.*)|*.*",
            Multiselect = true
        };
        if (openFileDialog.ShowDialog() != true)
        {
            return;
        }

        var settingsOptions = TextMateCodeRenderer.Settings.Options;

        foreach (var fileName in openFileDialog.FileNames)
        {
            var content = await File.ReadAllTextAsync(fileName);
            var extension = Path.GetExtension(fileName);
            var language = settingsOptions.GetLanguageByExtension(extension)?.Id ?? extension.TrimStart('.');

            var codeVm = new EditableCodeViewModel(content, extension, language)
            {
                FileLocation = fileName
            };

            var expander = new Expander
            {
                Content = codeVm,
                IsExpanded = true,
                Header = codeVm
            };

            expander.SetResourceReference(FrameworkElement.StyleProperty,
                TextMateCodeRenderer.EditCodeBlockStyleKey);

            var block = new BlockUIContainer(expander);

            if (richTextBox.CaretPosition.Paragraph != null)
            {
                richTextBox.CaretPosition = richTextBox.CaretPosition.InsertParagraphBreak();
            }

            var paragraph = richTextBox.CaretPosition.Paragraph;
            if (paragraph != null)
            {
                richTextBox.Document.Blocks.InsertBefore(paragraph, block);
            }
            else
            {
                richTextBox.Document.Blocks.Add(block);
            }

            richTextBox.CaretPosition = block.ElementEnd.GetInsertionPosition(LogicalDirection.Forward);
        }

        richTextBox.Focus();
        await ApplyText();
    }

    public override async Task ApplyText()
    {
        Content.Text = await GetPrompt() ?? string.Empty;
    }

    public override void AppendTempText(string tempText)
    {
        if (EditDocument.Blocks.LastBlock is Paragraph paragraph)
        {
            if (paragraph.Inlines.LastInline is Run run)
            {
                run.Text += tempText;
                return;
            }

            paragraph.Inlines.Add(new Run(tempText));
        }

        EditDocument.Blocks.Add(new Paragraph(new Run(tempText)));
    }
}