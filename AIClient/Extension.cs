using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Azure.AI.Inference;
using LLMClient.Render;
using Markdig;
using Markdig.Wpf;
using SkiaSharp;
using Svg.Skia;
using Color = System.Windows.Media.Color;

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

    public static async Task<ImageSource?> LoadImageAsync(this string url)
    {
        try
        {
            var extension = Path.GetExtension(url);
            var supportedImageExtensions = SupportedImageExtensions;
            if (!supportedImageExtensions.Contains(extension))
            {
                return null;
            }

            var fileInfo = HttpFileCache.Instance.GetFile(url);
            if (fileInfo != null)
            {
                await using (var fileStream = fileInfo.OpenRead())
                {
                    if (extension == ".svg")
                    {
                        return fileStream.SVGToImageSource();
                    }

                    return fileStream.ToImageSource();
                }
            }

            ImageSource? imageSource = null;
            using (var cancellationTokenSource = new CancellationTokenSource(5000))
            {
                var cancellationToken = cancellationTokenSource.Token;
                using (var message = await new HttpClient().GetAsync(url, cancellationToken))
                {
                    if (message.StatusCode == HttpStatusCode.OK)
                    {
                        using (var stream = await message.Content.ReadAsStreamAsync(CancellationToken.None))
                        {
                            if (extension == ".svg")
                            {
                                imageSource = stream.SVGToImageSource();
                            }
                            else
                            {
                                imageSource = stream.ToImageSource();
                            }

                            stream.Seek(0, SeekOrigin.Begin);
                            HttpFileCache.Instance.SaveContent(url, stream);
                            return imageSource;
                        }
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            Trace.Write($"read from {url} timeout");
        }
        catch (Exception e)
        {
            Trace.Write($"read from {url} exception: {e}");
        }

        return null;
    }

    private static readonly Lazy<string[]> SupportedImageExtensionsLazy = new Lazy<string[]>(() =>
    {
        string[] addition = new[] { ".svg" };
        ImageCodecInfo[] myCodecs = ImageCodecInfo.GetImageDecoders();
        if (myCodecs.Length == 0)
            return addition;
        return addition.Concat(myCodecs.SelectMany((info =>
        {
            return info.FilenameExtension?.Split(';').Select((s => s.TrimStart('*').ToLower())) ?? [];
        }))).ToArray();
    });

    public static string[] SupportedImageExtensions
    {
        get => SupportedImageExtensionsLazy.Value;
    }

    private static ImageSource ToImageSource(this Stream stream)
    {
        var image = new BitmapImage();
        image.BeginInit();
        image.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.UriSource = null;
        image.StreamSource = stream;
        image.EndInit();
        image.Freeze();
        return image;
    }

    private static ImageSource SVGToImageSource(this Stream stream)
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
            return mem.SVGToImageSource();
        }
    }

    // 递归查找子控件
    public static T? FindVisualChild<T>(this DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(parent, i);
            if (child is T t)
                return t;

            var childOfChild = FindVisualChild<T>(child);
            if (childOfChild != null)
                return childOfChild;
        }

        return null;
    }

    public static T? FindVisualParent<T>(this DependencyObject child) where T : DependencyObject
    {
        //get parent item
        DependencyObject? parentObject = VisualTreeHelper.GetParent(child);

        //we've reached the end of the tree
        if (parentObject == null) return null;

        //check if the parent matches the type we're looking for
        T? parent = parentObject as T;
        if (parent != null)
            return parent;
        else
            return FindVisualParent<T>(parentObject);
    }
}