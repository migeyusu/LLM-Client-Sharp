using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;


using LLMClient.Persistence;
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

    private void RenderImage(WpfRenderer renderer, LinkInline link, string url)
    {
        // 1. Prepare common UI components
        var template = new ControlTemplate(typeof(Button));
        template.VisualTree = new FrameworkElementFactory(typeof(ContentPresenter));

        var imageControl = new Image();
        imageControl.SetResourceReference(FrameworkElement.StyleProperty, Styles.ImageStyleKey);

        var btn = new Button
        {
            Template = template,
            Content = imageControl,
            Command = Commands.Image,
            CommandParameter = url,
            ToolTip = !string.IsNullOrEmpty(link.Title) ? link.Title : GetImageAltText(link)
        };

        var container = new BlockUIContainer(btn);
        var figure = new Figure
        {
            HorizontalAnchor = FigureHorizontalAnchor.PageCenter,
            VerticalAnchor = FigureVerticalAnchor.ParagraphTop,
            Width = new FigureLength(0, FigureUnitType.Auto)
        };
        figure.Blocks.Add(container);

        // 2. Add to renderer immediately
        renderer.WriteInline(figure);

        // 3. Load content
        if (url.IsBase64Image())
        {
            try
            {
                var source = ImageExtensions.GetImageSourceFromBase64(url);
                if (source != null)
                {
                    imageControl.Source = source;
                    btn.Height = source.Height;
                    btn.Width = source.Width;
                    return;
                }
            }
            catch (Exception ex)
            {
                Trace.TraceWarning($"Failed to load base64 image: {ex.Message}");
            }
            // Fallback if failed
            ShowFallback(url, link, container);
        }
        else
        {
            // Async load
            LoadImageAsync(url, link, imageControl, btn, container);
        }
    }

    private void ShowFallback(string url, LinkInline link, BlockUIContainer container)
    {
        var altText = GetImageAltText(link) ?? "[图片加载失败]";
        var textBlock = new TextBlock
        {
            Text = $"[{altText}]",
            ToolTip = $"无法加载图片: {url}"
        };
        container.Child = textBlock;
    }

    private async void LoadImageAsync(string url, LinkInline link, Image imageControl, Button btn, BlockUIContainer container)
    {
        try
        {
            if (Uri.TryCreate(url, UriKind.RelativeOrAbsolute, out var uri))
            {
                var imageSource = await uri.GetImageSourceAsync();
                if (imageSource != null)
                {
                    imageControl.Source = imageSource;
                    btn.Height = imageSource.Height;
                    btn.Width = imageSource.Width;
                    return;
                }
            }
        }
        catch (Exception e)
        {
            Trace.TraceWarning($"Failed to load image from URL '{url}': {e.Message}");
        }

        ShowFallback(url, link, container);
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