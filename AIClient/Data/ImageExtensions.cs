using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Drawing.Imaging;
using System.IO;
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
        return ToImageSource(PackIconKind.Web);
    });

    public static ThemedIcon APIIcon => APIIconImageLazy.Value;

    private static readonly Lazy<ThemedIcon> APIIconImageLazy = new Lazy<ThemedIcon>(() =>
    {
        return new LocalThemedIcon(ToImageSource(PackIconKind.Api));
    });

    private static ConcurrentDictionary<PackIconKind, ImageSource> _packIconCache
        = new ConcurrentDictionary<PackIconKind, ImageSource>();

    public static ImageSource ToImageSource(this PackIconKind kind, Brush? foreground = null)
    {
        return _packIconCache.GetOrAdd(kind, k =>
        {
            var packIcon = new PackIcon() { Kind = k };
            var packIconData = packIcon.Data;
            var geometry = Geometry.Parse(packIconData);
            foreground ??= Brushes.Black;
            var drawingImage =
                new DrawingImage(new GeometryDrawing(foreground, new Pen(Brushes.White, 0), geometry));
            drawingImage.Freeze();
            return drawingImage;
        });
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

        return new AsyncThemedIcon(async () => await GetImageAsync(lightUri) ?? APIIcon.CurrentSource,
            darkUri != null ? (async () => (await GetImageAsync(darkUri)) ?? APIIcon.CurrentSource) : null);
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

    private static readonly ConcurrentDictionary<Uri, Lazy<Task<ImageSource?>>> ImageCache =
        new ConcurrentDictionary<Uri, Lazy<Task<ImageSource?>>>();

    public static bool IsSupportedImageExtension(string extension)
    {
        return LocalSupportedImageExtensionsLazy.Value.Contains(extension, StringComparer.OrdinalIgnoreCase)
               || MagickNetSupportedImageExtensionsLazy.Value
                   .Contains(extension, StringComparer.OrdinalIgnoreCase);
    }

    public static Task<ImageSource?> GetImageAsync(this Uri uri)
    {
        return ImageCache.GetOrAdd(uri, u => new Lazy<Task<ImageSource?>>(() => CreateImageSourceAsync(u))).Value;
    }


    /// <summary>
    /// get image stream from uri, support pack, http(s), file and base64.
    /// <para>stream must correspond to a image format, otherwise throw NotSupportedException</para>
    /// </summary>
    /// <param name="uri"></param>
    /// <returns>stream and extension(like .png). stream must be disposed by caller.</returns>
    /// <exception cref="FileNotFoundException"></exception>
    /// <exception cref="Exception"></exception>
    /// <exception cref="ArgumentException"></exception>
    /// <exception cref="NotSupportedException"></exception>
    public static async Task<KeyValuePair<Stream, string>> GetImageStreamAsync(this Uri uri)
    {
        var uriScheme = uri.Scheme;
        Stream? stream = null;
        string extension = string.Empty;
        if (uri.IsAbsoluteUri)
        {
            if (uriScheme == "pack")
            {
                var resourceStream = Application.GetResourceStream(uri);
                if (resourceStream == null)
                {
                    throw new FileNotFoundException("Resource not found", uri.ToString());
                }

                extension = Path.GetExtension(uri.AbsolutePath);
                stream = resourceStream.Stream;
            }
            else if (uriScheme == Uri.UriSchemeHttp || uriScheme == Uri.UriSchemeHttps)
            {
                var fullPath = await HttpContentCache.Instance.GetOrCreateAsync(uri.AbsoluteUri);
                if (string.IsNullOrEmpty(fullPath))
                {
                    throw new Exception("Failed to download image from " + uri);
                }

                stream = File.OpenRead(fullPath);
                extension = Path.GetExtension(fullPath);
            }
            else if (uri.IsFile)
            {
                var localPath = uri.LocalPath;
                var fileInfo = new FileInfo(localPath);
                if (!fileInfo.Exists)
                {
                    throw new FileNotFoundException("File not found", localPath);
                }

                stream = fileInfo.OpenRead();
                extension = Path.GetExtension(localPath);
            }
        }
        else if (uri.OriginalString.StartsWith(Base64ImagePrefix))
        {
            if (!TryGetBinaryFromBase64(uri.ToString(), out var bytes, out var ext))
            {
                throw new ArgumentException("Invalid base64 image format", nameof(uri));
            }

            extension = "." + ext; // 添加点前缀以匹配扩展名
            stream = new MemoryStream(bytes);
        }

        if (stream == null)
        {
            throw new NotSupportedException("Unsupported URI scheme: " + uriScheme);
        }

        return new KeyValuePair<Stream, string>(stream, extension);
    }

    private static async Task<ImageSource?> CreateImageSourceAsync(Uri uri)
    {
        try
        {
            var (stream, extension) = await GetImageStreamAsync(uri);
            await using (stream)
            {
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
        // 如果发生异常，从缓存中移除失败的任务
        ImageCache.TryRemove(uri, out _);
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

    public const string Base64ImagePrefix = "data:image/";

    public static bool TryGetBinaryFromBase64(string base64Image, [NotNullWhen(true)] out byte[]? binary,
        [NotNullWhen(true)] out string? extension)
    {
        //parse data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAA...
        if (!base64Image.StartsWith(Base64ImagePrefix) || !base64Image.Contains("base64,"))
        {
            binary = null;
            extension = null;
            return false;
        }

        var length = Base64ImagePrefix.Length;
        extension = base64Image.Substring(length, base64Image.IndexOf(';') - length);
        var base64Data = base64Image.Substring(base64Image.IndexOf("base64,", StringComparison.Ordinal) + 7);
        binary = Convert.FromBase64String(base64Data);
        return true;
    }

    public static ImageSource GetImageSourceFromBase64(string base64Image)
    {
        if (!TryGetBinaryFromBase64(base64Image, out var bytes, out var extension))
        {
            throw new ArgumentException("Invalid base64 image format", nameof(base64Image));
        }

        using var stream = new MemoryStream(bytes);
        return stream.ToImageSource("." + extension);
    }

    public static ImageSource LoadSvgFromBase64(string src)
    {
        byte[] binaryData = Convert.FromBase64String(src);
        using (var mem = new MemoryStream(binaryData))
        {
            return mem.ToImageSource(".svg");
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="stream"></param>
    /// <param name="extension">like .png</param>
    /// <returns></returns>
    /// <exception cref="NotSupportedException"></exception>
    public static async Task<string> ToBase64StringAsync(this Stream stream, string extension)
    {
        extension = extension.TrimStart('.'); // remove leading dot if present
        //to data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAA...
        var streamLength = stream.Length;
        byte[] buffer = new byte[streamLength];
        stream.Position = 0;
        int readBytes = await stream.ReadAsync(buffer, 0, (int)streamLength);
        if (readBytes != streamLength)
        {
            throw new IOException("Failed to read the entire stream.");
        }

        var base64 = Convert.ToBase64String(buffer);
        return $"{Base64ImagePrefix}{extension.ToLower()};base64,{base64}";
    }
}