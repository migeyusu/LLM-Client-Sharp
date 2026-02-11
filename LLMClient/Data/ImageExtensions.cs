using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Drawing.Imaging;
using System.Globalization;
using System.Net.Http;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ImageMagick;
using LLMClient.Component.CustomControl;
using LLMClient.Endpoints;
using MaterialDesignThemes.Wpf;
using MimeTypes;
using Brushes = System.Windows.Media.Brushes;
using Pen = System.Windows.Media.Pen;
using Size = System.Drawing.Size;

namespace LLMClient.Data;

public static class ImageExtensions
{
    #region endpoint icon

    public static ImageSource EndpointIconImageLight => EndpointThemedIcon.LightModeSource;

    public static ImageSource EndpointIconImageDark => EndpointThemedIcon.DarkModeSource!;

    private static readonly Lazy<LocalThemedIcon> EndpointThemedIconLazy = new Lazy<LocalThemedIcon>(() =>
    {
        return LocalThemedIcon.FromPackIcon(PackIconKind.Web);
    });

    public static LocalThemedIcon EndpointThemedIcon => EndpointThemedIconLazy.Value;

    #endregion

    public static ImageSource APIIconImageLight => APIThemedIcon.LightModeSource;

    public static ImageSource APIIconImageDark => APIThemedIcon.DarkModeSource!;

    private static readonly Lazy<ThemedIcon> APIThemedIconLazy = new Lazy<ThemedIcon>(() =>
    {
        return LocalThemedIcon.FromPackIcon(PackIconKind.Api);
    });

    public static ThemedIcon APIThemedIcon => APIThemedIconLazy.Value;

    private static readonly ConcurrentDictionary<PackIconKindEntry, ImageSource> PackIconCache = new();

    private class PackIconKindEntry
    {
        public required PackIconKind Kind { get; init; }

        public required Brush Foreground { get; init; }

        public required Brush Background { get; init; }

        public override int GetHashCode()
        {
            return HashCode.Combine(Kind, Foreground, Background);
        }

        private bool Equals(PackIconKindEntry other)
        {
            return Kind == other.Kind && Foreground.Equals(other.Foreground) && Background.Equals(other.Background);
        }

        public override bool Equals(object? obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((PackIconKindEntry)obj);
        }

        private sealed class KindForegroundBackgroundEqualityComparer : IEqualityComparer<PackIconKindEntry>
        {
            public bool Equals(PackIconKindEntry? x, PackIconKindEntry? y)
            {
                if (ReferenceEquals(x, y)) return true;
                if (x is null) return false;
                if (y is null) return false;
                if (x.GetType() != y.GetType()) return false;
                return x.Kind == y.Kind && x.Foreground.Equals(y.Foreground) && x.Background.Equals(y.Background);
            }

            public int GetHashCode(PackIconKindEntry obj)
            {
                return HashCode.Combine((int)obj.Kind, obj.Foreground, obj.Background);
            }
        }

        public static IEqualityComparer<PackIconKindEntry> KindForegroundBackgroundComparer { get; } =
            new KindForegroundBackgroundEqualityComparer();
    }

    // 正则表达式用于匹配 Data URI 的头部
    // 格式如: data:image/png;base64,....
    private static readonly Regex DataUriPattern = new Regex(
        @"^data:(?<mimeType>(?<type>[\w\-\.]+)\/(?<ext>[\w\-\.\+]+));(?<encoding>\w+),(?<data>.*)", 
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    public static bool IsBase64Image(this string str)
    {
        return DataUriPattern.IsMatch(str);
    }

    public static bool TryGetBase64ImageInfo(this string str, [NotNullWhen(true)] out string? ext,
        [NotNullWhen(true)] out string? base64Data)
    {
        var match = DataUriPattern.Match(str);
        if (!match.Success)
        {
            ext = null;
            base64Data = null;
            return false;
        }

        ext = match.Groups["ext"].Value;
        base64Data = match.Groups["data"].Value;
        return true;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="kind"></param>
    /// <param name="foreground">default is black</param>
    /// <param name="background">default is white</param>
    /// <returns></returns>
    public static ImageSource ToImageSource(this PackIconKind kind, Brush? foreground = null, Brush? background = null)
    {
        foreground ??= Brushes.Black;
        background ??= Brushes.White;
        var packIconKindEntry = new PackIconKindEntry()
            { Kind = kind, Foreground = foreground, Background = background };
        return PackIconCache.GetOrAdd(packIconKindEntry, k =>
        {
            var packIcon = new PackIcon() { Kind = k.Kind };
            var packIconData = packIcon.Data;
            var geometry = Geometry.Parse(packIconData);
            var drawingImage =
                new DrawingImage(new GeometryDrawing(k.Foreground, new Pen(k.Background, 0), geometry)) { };
            drawingImage.Freeze();
            return drawingImage;
        });
    }

    public static ThemedIcon GetIcon(this ModelIconType iconType)
    {
        if (iconType == ModelIconType.None)
            return ImageExtensions.APIThemedIcon;
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

        return new AsyncThemedIcon(async () => await lightUri.GetImageSourceAsync() ?? APIThemedIcon.CurrentSource,
            darkUri != null
                ? (async () => (await darkUri.GetImageSourceAsync()) ?? APIThemedIcon.CurrentSource)
                : null);
    }

    public static LocalThemedIcon GetThemedIcon(this PackIconKind kind)
    {
        return LocalThemedIcon.FromPackIcon(kind);
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

    private static readonly ConcurrentDictionary<Uri, Lazy<Task<ImageSource?>>> ImageCache = new();

    public static bool IsSupportedImageExtension(string extension)
    {
        return LocalSupportedImageExtensionsLazy.Value.Contains(extension, StringComparer.OrdinalIgnoreCase)
               || MagickNetSupportedImageExtensionsLazy.Value
                   .Contains(extension, StringComparer.OrdinalIgnoreCase);
    }

    public static Task<ImageSource?> GetImageSourceAsync(this Uri uri)
    {
        return ImageCache.GetOrAdd(uri, u => new Lazy<Task<ImageSource?>>(() => CreateImageSourceAsync(u))).Value;
    }

/*System.Uri 类主要是为网络资源定位设计的（如 HTTP/HTTPS），由于历史原因和性能考量，它通常限制 URL 长度在 65519 或 65520 个字符左右（具体取决于 .NET 版本和配置，但在 .NET 6+ 中此限制依然存在于内部实现中）。
Base64 编码的图片非常容易超过这个长度（大约 48KB 的图片编码后就会达到 64KB 的字符限制）*/
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
            else if (uri.Scheme == "data" && uri.OriginalString.IsBase64Image())
            {
                // data URI scheme
                if (!TryGetBinaryFromBase64(uri.ToString(), out var bytes, out var ext))
                {
                    throw new ArgumentException("Invalid base64 image format", nameof(uri));
                }

                extension = "." + ext; // 添加点前缀以匹配扩展名
                stream = new MemoryStream(bytes);
            }
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
            var (stream, extension) = await uri.GetImageStreamAsync();
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

    public static ImageSource ToImageSource(this Stream stream, string extension, Size? size = null,
        bool shouldInvertColors = false)
    {
        //LocalSupportedImageExtensionsLazy.Value.Contains(extension, StringComparer.OrdinalIgnoreCase)
        if (extension.Equals(".ico"))
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
            var settings = new MagickReadSettings()
            {
                BackgroundColor = MagickColors.Transparent,
            };
            //自动获取大小
            if (size != null)
            {
                settings.Width = (uint)size.Value.Width;
                settings.Height = (uint)size.Value.Height;
            }

            var magickImage = new MagickImage(stream, settings);
            if (shouldInvertColors)
            {
                magickImage.Negate();
            }

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
        if (!base64Image.TryGetBase64ImageInfo(out extension, out var base64Data))
        {
            binary = null;
            extension = null;
            return false;
        }

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


    /// <summary>
    /// 从字符串生成一个包含指定文本的 ImageSource。
    /// </summary>
    /// <param name="text">要显示的文本。</param>
    /// <param name="fontFamily">字体族，例如 new FontFamily("Microsoft YaHei UI")。</param>
    /// <param name="fontSize">字体大小，单位是 DIP (Device Independent Pixels)。</param>
    /// <param name="textColor">文本颜色。</param>
    /// <param name="dpi">渲染的DPI，通常使用96.0。</param>
    /// <returns>一个可用的 ImageSource 对象。</returns>
    public static ImageSource CreateImageSourceFromText(
        string text,
        FontFamily fontFamily,
        double fontSize,
        Brush textColor,
        double dpi = 96.0)
    {
        var typeface = new Typeface(fontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
        var formattedText = new FormattedText(
            text,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            typeface,
            fontSize,
            textColor,
            null, // 可以传入一个 Brush 用于文本高亮
            VisualTreeHelper.GetDpi(Application.Current.MainWindow).PixelsPerDip // DPI缩放因子
        );

        var drawingVisual = new DrawingVisual();
        using (DrawingContext drawingContext = drawingVisual.RenderOpen())
        {
            drawingContext.DrawText(formattedText, new Point(0, 0));
        }

        var renderTargetBitmap = new RenderTargetBitmap(
            (int)Math.Ceiling(formattedText.Width),
            (int)Math.Ceiling(formattedText.Height),
            dpi,
            dpi,
            PixelFormats.Pbgra32 // 支持透明度的像素格式
        );
        renderTargetBitmap.Render(drawingVisual);
        renderTargetBitmap.Freeze();
        return renderTargetBitmap;
    }
}