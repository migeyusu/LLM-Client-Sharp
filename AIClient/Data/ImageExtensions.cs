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
using ImageMagick;
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
        return PackIconToSource(PackIconKind.Web);
    });

    public static ThemedIcon APIIcon => APIIconImageLazy.Value;

    private static readonly Lazy<ThemedIcon> APIIconImageLazy = new Lazy<ThemedIcon>(() =>
    {
        return new LocalThemedIcon(PackIconToSource(PackIconKind.Api));
    });

    public static ImageSource PackIconToSource(this PackIconKind kind)
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

    private static readonly Lazy<string[]> LocalSupportedImageExtensionsLazy = new Lazy<string[]>(() =>
    {
        var myCodecs = ImageCodecInfo.GetImageDecoders();
        return myCodecs.SelectMany(info =>
        {
            return info.FilenameExtension?.Split(';').Select((s => s.TrimStart('*').ToLower())) ?? [];
        }).ToArray();
    });

    private static readonly Lazy<string[]> MagickNetSupportedImageExtensionsLazy = new Lazy<string[]>(() =>
    {
        var supportedFormats = MagickNET.SupportedFormats;
        // 过滤并打印出所有可读写的格式
        return supportedFormats
            .Where(f => f.SupportsReading)
            .Select(f => "." + f.Format.ToString().ToLowerInvariant())
            .ToArray();
    });

    private static readonly ConcurrentDictionary<Uri, ImageSource?> IconCache =
        new ConcurrentDictionary<Uri, ImageSource?>();

    public static bool IsSupportedImageExtension(string extension)
    {
        return LocalSupportedImageExtensionsLazy.Value.Contains(extension, StringComparer.OrdinalIgnoreCase)
               || MagickNetSupportedImageExtensionsLazy.Value
                   .Contains(extension, StringComparer.OrdinalIgnoreCase);
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
            Stream stream;
            string fullPath;
            if (uriScheme == "pack" && uri.IsAbsoluteUri)
            {
                fullPath = uri.AbsolutePath;
                var resourceStream = Application.GetResourceStream(uri);
                if (resourceStream == null)
                {
                    throw new FileNotFoundException("Resource not found", uri.ToString());
                }

                stream = resourceStream.Stream;
            }
            else if (uriScheme == Uri.UriSchemeHttp || uriScheme == Uri.UriSchemeHttps)
            {
                fullPath = await HttpContentCache.Instance.GetOrCreateAsync(uri.AbsoluteUri);
                if (string.IsNullOrEmpty(fullPath))
                {
                    throw new Exception("Failed to download image from " + uri);
                }

                stream = File.OpenRead(fullPath);
            }
            else if (uri.IsFile)
            {
                fullPath = uri.LocalPath;
                var fileInfo = new FileInfo(fullPath);
                if (!fileInfo.Exists)
                {
                    throw new FileNotFoundException("File not found", fullPath);
                }

                stream = fileInfo.OpenRead();
            }
            else
            {
                throw new NotSupportedException("Unsupported URI scheme: " + uri.Scheme);
            }

            await using (stream)
            {
                var extension = Path.GetExtension(fullPath);
                return stream.ToImageSource(extension);
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

    public static ImageSource ToImageSource(this Stream stream, string extension, uint width = 32, uint height = 32)
    {
        if (LocalSupportedImageExtensionsLazy.Value.Contains(extension, StringComparer.OrdinalIgnoreCase))
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

        if (MagickNetSupportedImageExtensionsLazy.Value.Contains(extension,
                StringComparer.OrdinalIgnoreCase))

        {
            var magickImage = new MagickImage(stream, new MagickReadSettings()
            {
                Width = width,
                Height = height,
                BackgroundColor = MagickColors.Transparent,
            });
            var bitmapSource = magickImage.ToBitmapSource();
            bitmapSource.Freeze();
            return bitmapSource;
        }

        throw new NotSupportedException("Unsupported image extension: " + extension);
    }
}