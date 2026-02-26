using System.Windows;
using System.Windows.Documents;

namespace LLMClient.Component.Render;

public class TextmateColoredRun : Run
{
    public static readonly DependencyProperty ThemeColorsProperty = DependencyProperty.Register(
        nameof(ThemeColors), typeof(TextMateThemeColors), typeof(TextmateColoredRun),
        new FrameworkPropertyMetadata(null,
            new PropertyChangedCallback(ThemePropertyChangedCallback)));

    private static void ThemePropertyChangedCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TextmateColoredRun textmateRun && e.NewValue is TextMateThemeColors themeColors)
        {
            textmateRun.ApplyThemeStyle(themeColors);
        }
    }

    public TextMateThemeColors ThemeColors
    {
        get { return (TextMateThemeColors)GetValue(ThemeColorsProperty); }
        set { SetValue(ThemeColorsProperty, value); }
    }

    // 缓存 Scope 用于主题切换时重新计算颜色
    public List<string>? Scopes { get; }

    private readonly string? _scopesKey;

    private TokenStyle _currentStyle = TokenStyle.Default;

    public TextmateColoredRun()
    {
    }

    public TextmateColoredRun(string text, List<string> scopes) : base(text)
    {
        _scopesKey = string.Join("\u001F", scopes);
        this.Scopes = scopes;
    }

    public void ApplyThemeStyle(TextMateThemeColors textMateThemeColors)
    {
        if (this.Scopes == null || this._scopesKey == null)
        {
            return;
        }

        var style = textMateThemeColors.GetOrComputeStyle(_scopesKey, this.Scopes);
        if (style == _currentStyle) return;
        _currentStyle = style;
        var newBrush = textMateThemeColors.GetBrush(style.ForegroundId);
        if (!ReferenceEquals(Foreground, newBrush))
        {
            if (newBrush != null) Foreground = newBrush;
            else ClearValue(ForegroundProperty);
        }

        ApplyFontStyle(style.FontStyle);
    }

    private void ApplyFontStyle(TextMateSharp.Themes.FontStyle fontStyle)
    {
        var isNone = fontStyle is TextMateSharp.Themes.FontStyle.None or TextMateSharp.Themes.FontStyle.NotSet;

        // FontStyle (Italic)
        var newItalic = !isNone && fontStyle.HasFlag(TextMateSharp.Themes.FontStyle.Italic)
            ? FontStyles.Italic
            : FontStyles.Normal;
        if (FontStyle != newItalic) FontStyle = newItalic;

        // FontWeight (Bold)
        var newWeight = !isNone && fontStyle.HasFlag(TextMateSharp.Themes.FontStyle.Bold)
            ? FontWeights.Bold
            : FontWeights.Normal;
        if (FontWeight != newWeight) FontWeight = newWeight;

        // TextDecorations —— 仅支持单一装饰（Underline / Strikethrough 不共存）
        TextDecorationCollection? newDecorations = null;
        if (!isNone)
        {
            if (fontStyle.HasFlag(TextMateSharp.Themes.FontStyle.Underline))
                newDecorations = System.Windows.TextDecorations.Underline;
            else if (fontStyle.HasFlag(TextMateSharp.Themes.FontStyle.Strikethrough))
                newDecorations = System.Windows.TextDecorations.Strikethrough;
        }

        // TextDecorationCollection 是引用类型，WPF 内置集合实例可用 ReferenceEquals 比较
        if (!ReferenceEquals(TextDecorations, newDecorations))
            TextDecorations = newDecorations;
    }
}