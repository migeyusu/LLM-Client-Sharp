using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media.Imaging;
using LLMClient.Data;
using Markdig.Renderers;
using Markdig.Renderers.Wpf.Inlines;
using Markdig.Syntax.Inlines;
using Markdig.Wpf;

namespace LLMClient.UI.Render;

public class LinkInlineRendererEx : LinkInlineRenderer
{
    protected override void Write(WpfRenderer renderer, LinkInline link)
    {
        if (renderer == null) throw new ArgumentNullException(nameof(renderer));
        if (link == null) throw new ArgumentNullException(nameof(link));

        var url = link.GetDynamicUrl != null ? link.GetDynamicUrl() ?? link.Url : link.Url;

        if (!Uri.IsWellFormedUriString(url, UriKind.RelativeOrAbsolute))
        {
            url = "#";
        }

        if (link.IsImage)
        {
            var template = new ControlTemplate();
            var image = new FrameworkElementFactory(typeof(Image));
            if (url.StartsWith(ImageExtensions.Base64ImagePrefix))
            {
                try
                {
                    var imageSource = ImageExtensions.GetImageSourceFromBase64(url);
                    image.SetValue(Image.SourceProperty, imageSource);
                }
                catch (Exception e)
                {
                    Trace.TraceWarning("Invalid base64 image data: {0}", e.Message);
                }
            }
            else if (Uri.TryCreate(url, UriKind.RelativeOrAbsolute, out var uri))
            {
                image.SetValue(Image.SourceProperty, new BitmapImage(uri));
            }
            else
            {
                //image stream ![Point Dash Brush Visual Effect](data:image/png;base64,
                Trace.TraceWarning("Invalid image URL: {0}", url);
            }

            image.SetResourceReference(FrameworkContentElement.StyleProperty, Styles.ImageStyleKey);
            template.VisualTree = image;
            var btn = new Button()
            {
                Template = template,
                Command = Commands.Image,
                CommandParameter = url
            };

            renderer.WriteInline(new InlineUIContainer(btn));
        }
        else
        {
            var hyperlink = new Hyperlink
            {
                Command = Commands.Hyperlink,
                CommandParameter = url,
                NavigateUri = new Uri(url, UriKind.RelativeOrAbsolute),
                ToolTip = !string.IsNullOrEmpty(link.Title) ? link.Title : null,
            };

            hyperlink.SetResourceReference(FrameworkContentElement.StyleProperty, Styles.HyperlinkStyleKey);

            renderer.Push(hyperlink);
            renderer.WriteChildren(link);
            renderer.Pop();
        }
    }
}