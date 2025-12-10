using System.Globalization;
using System.Windows.Media;
using TextMateSharp.Themes;

namespace LLMClient.Component.Render;

public class TextMateThemeColors
{
    public Theme Theme { get; set; }

    private Dictionary<int, Brush> _brushes = new Dictionary<int, Brush>();

    public TextMateThemeColors(Theme theme)
    {
        Theme = theme;
    }


    public Brush? GetBrush(int colorId)
    {
        if (colorId == -1)
            return null;
        if (!_brushes.TryGetValue(colorId, out var brush))
        {
            var hexString = this.Theme.GetColor(colorId);
            if (hexString.IndexOf('#') != -1)
                hexString = hexString.Replace("#", "");
            var r = byte.Parse(hexString.Substring(0, 2), NumberStyles.AllowHexSpecifier);
            var g = byte.Parse(hexString.Substring(2, 2), NumberStyles.AllowHexSpecifier);
            var b = byte.Parse(hexString.Substring(4, 2), NumberStyles.AllowHexSpecifier);
            var fromRgb = Color.FromRgb(r, g, b);
            brush = new SolidColorBrush(fromRgb);
            brush.Freeze();
            _brushes.Add(colorId, brush);
        }

        return brush;
    }
}