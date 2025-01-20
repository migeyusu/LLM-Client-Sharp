using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
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
    public static ImageSource LoadImage(string src)
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