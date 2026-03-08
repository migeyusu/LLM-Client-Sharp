using System.Text;
using System.Windows.Controls;
using System.Windows.Documents;
using LLMClient.Component.Render;
using LLMClient.Component.Utility;
using Markdig.Renderers.Html;
using Microsoft.Extensions.AI;

namespace LLMClient.Dialog;

public class TextContentCodeEditViewModel : TextContentEditViewModel
{
    public Task<string?> GetPersistString()
    {
        return GetPrompt(true);
    }

    public async Task<string?> GetPrompt(bool includeFilePath = false)
    {
        var doc = await WaitAsyncProperty<FlowDocument?>(nameof(Document));
        if (doc == null)
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

    public FlowDocument? Document
    {
        get
        {
            return GetAsyncProperty(async () =>
            {
                if (string.IsNullOrEmpty(_originalText))
                {
                    return null;
                }

                var flowDocument = new FlowDocument();
                var renderer = CustomMarkdownRenderer.EditRenderer(flowDocument);
                await renderer.RenderMarkdown(_originalText);
                return flowDocument;
            });
        }
    }

    private string? _originalText;

    public TextContentCodeEditViewModel(TextContent textContent, string? messageId) : base(textContent, messageId)
    {
        this._originalText = textContent.Text;
    }

    protected override void Rollback()
    {
        TextContent.Text = _originalText;
        this.HasEdit = false;
    }

    public override bool Check()
    {
        if (string.IsNullOrEmpty(GetPrompt().Result))
        {
            MessageEventBus.Publish($"{MessageId}：文本内容不能为空");
            return false;
        }

        return true;
    }

    public override async Task ApplyText()
    {
        if (!HasEdit)
        {
            return;
        }

        TextContent.Text = await GetPrompt() ?? "";
    }
}