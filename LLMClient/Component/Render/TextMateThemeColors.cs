using System.Collections.Concurrent;
using System.Globalization;
using System.Windows.Media;
using TextMateSharp.Themes;

namespace LLMClient.Component.Render;

public readonly record struct TokenStyle(int ForegroundId, TextMateSharp.Themes.FontStyle FontStyle)
{
    public static readonly TokenStyle Default = new(-1, TextMateSharp.Themes.FontStyle.None);
}

public class TextMateThemeColors
{
    private Theme Theme { get; }

    private Dictionary<int, Brush> _brushes = new();

    private readonly ConcurrentDictionary<string, TokenStyle> _matchCache = new(StringComparer.Ordinal);

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

    /// <summary>
    /// 获取或计算指定 Scope 的样式（带缓存）
    /// </summary>
    public TokenStyle GetOrComputeStyle(string scopesKey, List<string> scopes)
    {
        if (string.IsNullOrEmpty(scopesKey))
            return TokenStyle.Default;

        return _matchCache.GetOrAdd(scopesKey, key =>
        {
            int foreground = -1;
            var fontStyle = FontStyle.None;

            var rules = Theme.Match(scopes);
            foreach (var rule in rules)
            {
                if (foreground == -1 && rule.foreground > 0)
                    foreground = rule.foreground;

                if (rule.fontStyle > 0)
                    fontStyle |= (FontStyle)rule.fontStyle;
            }

            return new TokenStyle(foreground, fontStyle);
        });
    }

    /// <summary>
    /// 当主题切换时清空缓存（可选，如果主题对象变了其实不需要）
    /// </summary>
    public void ClearCache()
    {
        _matchCache.Clear();
    }
}

// 新增 EventArgs
public class ThemeChangedEventArgs : EventArgs
{
    public TextMateThemeColors NewTheme { get; }
    public ThemeChangedEventArgs(TextMateThemeColors newTheme) => NewTheme = newTheme;
}