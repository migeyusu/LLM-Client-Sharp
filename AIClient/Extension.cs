using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
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

public static class UITheme
{
    private static bool _isDarkMode = false;

    public static bool IsDarkMode
    {
        get => _isDarkMode;
        set
        {
            if (value == _isDarkMode)
            {
                return;
            }

            _isDarkMode = value;
            OnModeChanged(value);
        }
    }

    public static event Action<bool>? ModeChanged;

    private static void OnModeChanged(bool obj)
    {
        ModeChanged?.Invoke(obj);
    }
}

public static class Extension
{
    private static readonly CustomRenderer Renderer;

    private static readonly MarkdownPipeline DefaultPipeline =
        new MarkdownPipelineBuilder().UseSupportedExtensions().Build();

    static Extension()
    {
        var config = GlobalConfig.LoadOrCreate();
        Renderer = new CustomRenderer() { ThemeName = config.ThemeName };
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

    public static ImageSource? LoadSvgFromHttp(string logoUrl)
    {
        try
        {
            using (var message = new HttpClient().GetAsync($"https://github.com{logoUrl}").GetAwaiter().GetResult())
            {
                if (message.StatusCode == HttpStatusCode.OK)
                {
                    using (var stream = message.Content.ReadAsStream())
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
                }
            }
        }
        catch (Exception)
        {
            return null;
        }

        return null;
    }


    public static ImageSource LoadSvgFromBase64(string src)
    {
        //data:image/svg;base64,
        byte[] binaryData = Convert.FromBase64String(src);
        using (var mem = new MemoryStream(binaryData))
        {
            var skBitmap = new SKBitmap();
            var skCanvas = new SKCanvas(skBitmap);
            var skSvg = new SKSvg();
            skSvg.Load(mem);
            skCanvas.DrawPicture(skSvg.Picture);
            using (var asStream = new MemoryStream())
            {
                skSvg.Save(asStream, SKColor.Empty);
                mem.Position = 0;
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
    }
}