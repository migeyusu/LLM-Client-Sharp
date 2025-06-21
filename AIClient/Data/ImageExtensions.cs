using System.Collections.Concurrent;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LLMClient.Endpoints;
using LLMClient.UI.Component;
using MaterialDesignThemes.Wpf;
using MimeTypes;
using Brushes = System.Windows.Media.Brushes;
using Pen = System.Windows.Media.Pen;

namespace LLMClient.Data;

public static class ImageExtensions
{
    public static ImageSource EndpointIcon => EndpointIconImageLazy.Value;

    private static readonly Lazy<ImageSource> EndpointIconImageLazy = new Lazy<ImageSource>(() =>
    {
        return PackIconToIcon(PackIconKind.Web);
    });

    public static ThemedIcon APIIcon => APIIconImageLazy.Value;

    private static readonly Lazy<ThemedIcon> APIIconImageLazy = new Lazy<ThemedIcon>(() =>
    {
        return new LocalThemedIcon(PackIconToIcon(PackIconKind.Api));
    });

    private static ImageSource PackIconToIcon(PackIconKind kind)
    {
        var packIcon = new PackIcon() { Kind = kind };
        var packIconData = packIcon.Data;
        var geometry = Geometry.Parse(packIconData);
        var drawingImage =
            new DrawingImage(new GeometryDrawing(Brushes.Black, new Pen(Brushes.White, 0), geometry));
        drawingImage.Freeze();
        return drawingImage;
    }

    public static ThemedIcon GetIcon(ModelIconType iconType)
    {
        if (iconType == ModelIconType.None)
            return ImageExtensions.APIIcon;
        //获取icontype的Attribute
        var darkModeAttribute =
            typeof(ModelIconType).GetField(iconType.ToString())?.GetCustomAttribute<DarkModeAttribute>();
        Uri lightUri;
        Uri? darkUri = null;
        if (darkModeAttribute != null)
        {
            darkUri = new Uri(
                $"pack://application:,,,/LLMClient;component/Resources/Images/llm/{iconType.ToString().ToLower()}-dark.png",
                UriKind.Absolute);
            lightUri = new Uri(
                $"pack://application:,,,/LLMClient;component/Resources/Images/llm/{iconType.ToString().ToLower()}-light.png",
                UriKind.Absolute);
        }
        else
        {
            lightUri = new Uri(
                $"pack://application:,,,/LLMClient;component/Resources/Images/llm/{iconType.ToString().ToLower()}.png"
                , UriKind.Absolute);
        }

        return new LocalThemedIcon(GetIcon(lightUri).Result ?? APIIcon.CurrentSource,
            darkUri != null ? GetIcon(darkUri).Result : null);
    }

    private static readonly Lazy<string[]> SupportedImageExtensionsLazy = new Lazy<string[]>(() =>
    {
        var addition = new[] { ".svg" };
        var myCodecs = ImageCodecInfo.GetImageDecoders();
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

    private static readonly ConcurrentDictionary<Uri, ImageSource?> IconCache =
        new ConcurrentDictionary<Uri, ImageSource?>();

    public static bool IsSupportedImageExtension(string extension)
    {
        return SupportedImageExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
    }

    public static async Task<ImageSource?> GetIcon(this Uri uri)
    {
        try
        {
            if (IconCache.TryGetValue(uri, out var value))
            {
                return value;
            }

            var uriScheme = uri.Scheme;
            if (uriScheme == "pack" && uri.IsAbsoluteUri)
            {
                var absolutePath = uri.AbsolutePath;
                var extension = Path.GetExtension(absolutePath);
                if (!IsSupportedImageExtension(extension))
                {
                    return null;
                }

                return IconCache.GetOrAdd(uri,
                    (uri1 =>
                    {
                        var resourceStream = Application.GetResourceStream(uri);
                        if (resourceStream == null)
                        {
                            throw new Exception("Resource stream not found");
                        }

                        var result = new BitmapImage(uri);
                        result.Freeze();
                        return result;
                    }));
            }

            if (uriScheme == Uri.UriSchemeHttp || uriScheme == Uri.UriSchemeHttps)
            {
                return await FromHttp(uri);
            }

            if (uri.IsFile)
            {
                //对于文件，首先检查是否位于cache/images文件夹内，如果否，则先拷贝到该文件夹
                var filePath = uri.LocalPath;
                var extension = Path.GetExtension(filePath);
                if (!IsSupportedImageExtension(extension))
                {
                    return null;
                }

                var fileInfo = new FileInfo(filePath);
                if (!fileInfo.Exists)
                {
                    return null;
                }

                await using (var fileStream = fileInfo.OpenRead())
                {
                    return extension == ".svg"
                        ? fileStream.SVGStreamToImageSource()
                        : fileStream.ToDefaultImageSource();
                }
            }
        }
        catch (OperationCanceledException)
        {
            Trace.Write($"read from {uri} timeout.");
        }
        catch (Exception e)
        {
            Trace.Write($"read from {uri} exception: {e}.");
        }

        Trace.WriteLine($"read {uri} failed,not match any scheme.");
        return null;
    }
    
    private static async Task<ImageSource?> FromHttp(Uri uri)
    {
        var url = uri.AbsoluteUri;
        string filePath;
        try
        {
            filePath = await HttpContentCache.Instance.GetOrCreateAsync(url);
        }
        catch (Exception e)
        {
            Trace.WriteLine(e.Message);
            return null;
        }

        var extension = Path.GetExtension(filePath);
        if (!IsSupportedImageExtension(extension))
        {
            return null;
        }

        return IconCache.GetOrAdd(uri, (uri1) =>
        {
            var fileInfo = new FileInfo(filePath);
            using (var fileStream = fileInfo.OpenRead())
            {
                var result = extension == ".svg"
                    ? fileStream.SVGStreamToImageSource()
                    : fileStream.ToDefaultImageSource();
                return result;
            }
        });
    }

    public static async Task<(bool Supported, string? Extension)> CheckImageSupportAsync(string url)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Head, url);
            using (var httpClient = new HttpClient())
            {
                using var response = await httpClient.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead
                );
                response.EnsureSuccessStatusCode();

                // 从Content-Type获取类型
                var contentType = response.Content.Headers.ContentType;
                if (contentType == null)
                    return (false, null);

                // 获取扩展名
                string? extension = MimeTypeMap.GetExtension(contentType.MediaType, false);

                // 回退到URL扩展名
                if (string.IsNullOrEmpty(extension))
                    extension = Path.GetExtension(new Uri(url).AbsolutePath);

                // 最终检查
                if (string.IsNullOrEmpty(extension))
                    return (false, null);

                return (
                    IsSupportedImageExtension(extension), extension
                );
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"支持检查失败: {ex.Message}");
            return (false, null);
        }
    }


    public static ImageSource ToDefaultImageSource(this Stream stream)
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
}