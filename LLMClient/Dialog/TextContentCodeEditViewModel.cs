using System.Text;
using System.Windows.Controls;
using System.Windows.Documents;
using LLMClient.Component.Render;
using LLMClient.Component.Utility;
using Markdig.Renderers.Html;
using Microsoft.Win32;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Windows.Services.Maps.LocalSearch;
using CommunityToolkit.Mvvm.Input;
using LLMClient.Data;
using Microsoft.SemanticKernel;
using Microsoft.Xaml.Behaviors.Core;
using TextContent = Microsoft.Extensions.AI.TextContent;

namespace LLMClient.Dialog;

public class TextContentCodeEditViewModel : TextContentEditViewModel
{
    public ICommand PastCommand { get; }

    public Task<string?> GetPersistString()
    {
        return GetPrompt(true);
    }

    public async Task<string?> GetPrompt(bool includeFilePath = true)
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

                sb.AppendLine();
                sb.AppendLine(codeVm.Code);
                sb.AppendLine("```");
            }
            // 情况 B：图片容器（粘贴产生的），暂不支持
            /*else if (block is BlockUIContainer imgContainer && imgContainer.Child is Image img)
            {
                if (img.Source is BitmapSource bitmapSource)
                {
                    /*if (!string.IsNullOrEmpty(base64))
                    {
                        // 构造 Microsoft.Extensions.AI 的图片内容
                        contents.Add(new ImageContent(base64, "image/png"));
                    }#1#
                }
            }*/
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
                Document.Blocks.Clear();
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

    private readonly Lazy<FlowDocument> _docLazy = new(() => new FlowDocument());
    private FlowDocument Document => _docLazy.Value;

    public TextContentCodeEditViewModel(TextContent textContent, string? messageId) : base(textContent, messageId)
    {
        PastCommand = new RelayCommand<ExecutedRoutedEventArgs>((args =>
        {
            if (args == null)
            {
                return;
            }

            var objSource = args.Source;
            var richTextBox = FindRichTextBox(objSource);
            if (richTextBox == null)
            {
                return;
            }

            if (Clipboard.ContainsText())
            {
                var text = Clipboard.GetText();
                if (IsLikelyCode(text))
                {
                    InsertCodeBlock(richTextBox, text, ".txt", "plaintext", string.Empty);
                }
                else
                {
                    richTextBox.Paste();
                }
                args.Handled = true;
            }
            else if (Clipboard.ContainsFileDropList())
            {
                AddCodeFile(objSource);
                args.Handled = true;
            }
        }));
    }

    /// <summary>
    /// 简单但有效的代码判断（可自行扩展）
    /// </summary>
    private static bool IsLikelyCode(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        var lines = text.Split('\n');
        return lines.Length >= 3 && // 多行
               text.Contains('{') && text.Contains('}');
    }

    private static RichTextBox? FindRichTextBox(object? obj)
    {
        if (obj is RichTextBox rtb)
        {
            return rtb;
        }

        if (obj is DependencyObject dependencyObject)
        {
            return dependencyObject.FindVisualChild<RichTextBox>();
        }

        return null;
    }

    protected override async void AddCodeFile(object? o)
    {
        var richTextBox = FindRichTextBox(o);
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

        await InsertFilesAsTexts(openFileDialog.FileNames, richTextBox);
    }

    public override async Task ApplyText()
    {
        Content.Text = await GetPrompt(true) ?? string.Empty;
        InvalidateAsyncProperty(nameof(EditDocument));
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

    public override async void DropFiles(IEnumerable<string> filePaths, object? o)
    {
        if (o is RichTextBox richTextBox)
        {
            await InsertFilesAsTexts(filePaths, richTextBox);
        }
    }

    private async Task InsertFilesAsTexts(IEnumerable<string> fileNames, RichTextBox richTextBox)
    {
        var settingsOptions = TextMateCodeRenderer.Settings.Options;
        foreach (var fileName in fileNames)
        {
            var extension = Path.GetExtension(fileName);
            if (ImageExtensions.IsSupportedImageExtension(extension))
            {
                MessageEventBus.Publish($"不支持的文件类型: {extension}，仅支持文本文件。");
                return;
            }

            var content = await File.ReadAllTextAsync(fileName);
            var language = settingsOptions.GetLanguageByExtension(extension)?.Id ?? extension.TrimStart('.');
            InsertCodeBlock(richTextBox, content, extension, language, fileName);
        }

        richTextBox.Focus();
        await ApplyText();
    }

    private void InsertCodeBlock(RichTextBox richTextBox, string content, string extension, string language,
        string fileName)
    {
        var codeVm = new EditableCodeViewModel(content, extension, language)
        {
            FileLocation = fileName
        };

        var expander = new Expander
        {
            Content = codeVm,
            IsExpanded = false,
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
}