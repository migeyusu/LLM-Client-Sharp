using System.Collections.Concurrent;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LLMClient.Endpoints;
using LLMClient.UI.Component;
using MaterialDesignThemes.Wpf;

namespace LLMClient.Data;

public static class Icons
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
            return Icons.APIIcon;
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

    private static readonly ConcurrentDictionary<Uri, ImageSource?> IconCache =
        new ConcurrentDictionary<Uri, ImageSource?>();

    public static async Task<ImageSource?> GetIcon(this Uri uri)
    {
        ImageSource? result = null;
        try
        {
            var absolutePath = uri.AbsolutePath;
            var extension = Path.GetExtension(absolutePath);
            var supportedImageExtensions = SupportedImageExtensions;
            if (!supportedImageExtensions.Contains(extension))
            {
                return result;
            }

            if (IconCache.TryGetValue(uri, out var value))
            {
                return value;
            }

            var uriScheme = uri.Scheme;
            if (uriScheme == "pack" && uri.IsAbsoluteUri)
            {
                var resourceStream = Application.GetResourceStream(uri);
                if (resourceStream != null)
                {
                    result = new BitmapImage(uri);
                    result.Freeze();
                    IconCache.TryAdd(uri, result);
                }
            }
            else if (uriScheme == Uri.UriSchemeHttp || uriScheme == Uri.UriSchemeHttps)
            {
                var url = uri.AbsoluteUri;
                var fileInfo = HttpFileCache.Instance.GetFile(url);
                if (fileInfo != null)
                {
                    await using (var fileStream = fileInfo.OpenRead())
                    {
                        result = extension == ".svg" ? fileStream.SVGStreamToImageSource() : fileStream.ToImageSource();
                        IconCache.TryAdd(uri, result);
                        return result;
                    }
                }

                using (var cancellationTokenSource = new CancellationTokenSource(5000))
                {
                    var cancellationToken = cancellationTokenSource.Token;
                    using (var message = await new HttpClient().GetAsync(url, cancellationToken))
                    {
                        if (message.StatusCode == HttpStatusCode.OK)
                        {
                            using (var stream = await message.Content.ReadAsStreamAsync(CancellationToken.None))
                            {
                                result = extension == ".svg" ? stream.SVGStreamToImageSource() : stream.ToImageSource();

                                IconCache.TryAdd(uri, result);
                                stream.Seek(0, SeekOrigin.Begin);
                                HttpFileCache.Instance.SaveContent(url, stream);
                                return result;
                            }
                        }
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            Trace.Write($"read from {uri} timeout");
        }
        catch (Exception e)
        {
            Trace.Write($"read from {uri} exception: {e}");
        }

        return result;
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
}

public class EnumToIconConverter : IValueConverter
{
    public static EnumToIconConverter Instance;

    static EnumToIconConverter()
    {
        Instance = new EnumToIconConverter();
    }

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is ModelIconType iconType)
        {
            return Icons.GetIcon(iconType).CurrentSource;
        }

        return null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}