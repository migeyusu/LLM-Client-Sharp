using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Azure.AI.Inference;
using LLMClient.Render;
using Markdig;
using Markdig.Wpf;
using SkiaSharp;
using Svg.Skia;

namespace LLMClient;

public static class Extension
{
    private static readonly CustomRenderer Renderer;

    private static readonly MarkdownPipeline DefaultPipeline =
        new MarkdownPipelineBuilder().UseSupportedExtensions().Build();

    static Extension()
    {
        Renderer = new CustomRenderer();
        Renderer.Initialize();
        DefaultPipeline.Setup(Renderer);
    }

    public static void UpgradeAPIVersion(this ChatCompletionsClient client, string apiVersion = "2024-12-01-preview")
    {
        var propertyInfo = client.GetType().GetField("_apiVersion", BindingFlags.Instance | BindingFlags.NonPublic);
        propertyInfo?.SetValue(client, apiVersion);
    }

    public static FlowDocument ToFlowDocument(this string Raw)
    {
        return Markdig.Wpf.Markdown.ToFlowDocument(Raw, DefaultPipeline, Renderer);
    }

    public static async Task<ImageSource?> LoadSvgFromHttp(string logoUrl)
    {
        try
        {
            var requestUri = $"https://github.com{logoUrl}";
            var fileInfo = HttpFileCache.Instance.GetFile(requestUri);
            if (fileInfo != null)
            {
                await using (var fileStream = fileInfo.OpenRead())
                {
                    return fileStream.ToImageSource();
                }
            }

            using (var cancellationTokenSource = new CancellationTokenSource(5000))
            {
                var cancellationToken = cancellationTokenSource.Token;
                using (var message = await new HttpClient().GetAsync(requestUri, cancellationToken))
                {
                    if (message.StatusCode == HttpStatusCode.OK)
                    {
                        using (var stream = await message.Content.ReadAsStreamAsync(CancellationToken.None))
                        {
                            var imageSource = stream.ToImageSource();
                            stream.Seek(0, SeekOrigin.Begin);
                            HttpFileCache.Instance.SaveContent(requestUri, stream);
                            return imageSource;
                        }
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            Trace.Write($"read from {logoUrl} timeout");
        }
        catch (Exception e)
        {
            Trace.Write($"read from {logoUrl} exception: {e}");
        }

        return null;
    }

    private static ImageSource ToImageSource(this Stream stream)
    {
        var skBitmap = new SKBitmap();
        var skCanvas = new SKCanvas(skBitmap);
        var skSvg = new SKSvg();
        skSvg.Load(stream);
        skCanvas.DrawPicture(skSvg.Picture);
        using (var asStream = new MemoryStream())
        {
            skSvg.Save(asStream, SKColor.Empty);
            stream.Position = 0;
            var image = new BitmapImage();
            image.BeginInit();
            image.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.UriSource = null;
            image.StreamSource = asStream;
            image.EndInit();
            image.Freeze();
            return image;
        }
    }


    public static ImageSource LoadSvgFromBase64(string src)
    {
        //data:image/svg;base64,
        byte[] binaryData = Convert.FromBase64String(src);
        using (var mem = new MemoryStream(binaryData))
        {
            return mem.ToImageSource();
        }
    }
}