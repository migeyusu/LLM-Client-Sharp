using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LLMClient.Data;
using Markdig.Renderers;
using Markdig.Renderers.Wpf.Inlines;
using Markdig.Syntax.Inlines;
using Markdig.Wpf;
using Inline = Markdig.Syntax.Inlines.Inline;

namespace LLMClient.Component.Render;

public class LinkInlineRendererEx : LinkInlineRenderer
{
    protected override void Write(WpfRenderer renderer, LinkInline link)
    {
        if (renderer == null) throw new ArgumentNullException(nameof(renderer));
        if (link == null) throw new ArgumentNullException(nameof(link));

        var url = link.GetDynamicUrl?.Invoke() ?? link.Url;
        // 处理空 URL 的情况
        if (string.IsNullOrWhiteSpace(url))
        {
            // 没有 URL，仅渲染文本内容
            renderer.WriteChildren(link);
            return;
        }

        if (link.IsImage)
        {
            RenderImage(renderer, link, url);
        }
        else
        {
            RenderHyperlink(renderer, link, url);
        }
    }

    private async void RenderImage(WpfRenderer renderer, LinkInline link, string url)
    {
        ImageSource? imageSource = null;

        try
        {
            if (url.IsBase64Image())
            {
                imageSource = ImageExtensions.GetImageSourceFromBase64(url);
            }
            else
            {
                if (Uri.TryCreate(url, UriKind.RelativeOrAbsolute, out var uri))
                {
                    imageSource = await uri.GetImageSourceAsync();
                }
            }
        }
        catch (Exception e)
        {
            Trace.TraceWarning($"Failed to load image from URL '{url}': {e.Message}");
        }

        // 如果图片加载失败，显示替代文本
        if (imageSource == null)
        {
            // 创建一个 TextBlock 显示 alt 文本
            var altText = GetImageAltText(link) ?? "[图片加载失败]";
            var textBlock = new TextBlock
            {
                Text = $"[{altText}]",
                ToolTip = $"无法加载图片: {url}"
            };
            renderer.WriteInline(new InlineUIContainer(textBlock));
            return;
        }

        // 渲染图片
        var template = new ControlTemplate();
        var image = new FrameworkElementFactory(typeof(Image));
        image.SetValue(Image.SourceProperty, imageSource);
        image.SetResourceReference(FrameworkContentElement.StyleProperty, Styles.ImageStyleKey);
        template.VisualTree = image;

        var btn = new Button
        {
            Template = template,
            Command = Commands.Image,
            CommandParameter = url,
            Height = imageSource.Height,
            Width = imageSource.Width,
            // 设置 ToolTip：优先使用 title，其次使用 alt 文本
            ToolTip = !string.IsNullOrEmpty(link.Title)
                ? link.Title
                : GetImageAltText(link)
        };
        var figure = new Figure()
        {
            HorizontalAnchor = FigureHorizontalAnchor.PageCenter,
            VerticalAnchor = FigureVerticalAnchor.ParagraphTop,
            Width = new FigureLength(0, FigureUnitType.Auto) // 自动宽度
        };
        figure.Blocks.Add(new BlockUIContainer(btn));
        renderer.WriteInline(figure);
    }

    private void RenderHyperlink(WpfRenderer renderer, LinkInline link, string url)
    {
        // 验证 URL 格式
        if (!Uri.TryCreate(url, UriKind.RelativeOrAbsolute, out var uri))
        {
            // URL 格式不正确，仅渲染文本内容，不创建超链接
            Trace.TraceWarning($"Invalid URL format: {url}");
            renderer.WriteChildren(link);
            return;
        }

        // 创建超链接
        var hyperlink = new Hyperlink
        {
            Command = Commands.Hyperlink,
            CommandParameter = url,
            NavigateUri = uri,
            ToolTip = !string.IsNullOrEmpty(link.Title) ? link.Title : url,
        };

        hyperlink.SetResourceReference(FrameworkContentElement.StyleProperty, Styles.HyperlinkStyleKey);

        renderer.Push(hyperlink);
        renderer.WriteChildren(link);
        renderer.Pop();
    }

    /// <summary>
    /// 从 LinkInline 中提取图片的替代文本（alt text）
    /// </summary>
    private string? GetImageAltText(LinkInline link)
    {
        // 遍历子元素提取文本
        var altTextBuilder = new System.Text.StringBuilder();
        ExtractText(link.FirstChild, altTextBuilder);
        var result = altTextBuilder.ToString().Trim();
        return string.IsNullOrEmpty(result) ? null : result;
    }

    /// <summary>
    /// 递归提取内联元素中的文本
    /// </summary>
    private void ExtractText(Inline? inline, System.Text.StringBuilder builder)
    {
        while (inline != null)
        {
            if (inline is LiteralInline literal)
            {
                builder.Append(literal.Content.ToString());
            }
            else if (inline is ContainerInline container)
            {
                ExtractText(container.FirstChild, builder);
            }

            inline = inline.NextSibling;
        }
    }
}