using System.Diagnostics;
using System.Windows;
using System.Windows.Documents;
using TextMateSharp.Grammars;

namespace LLMClient.UI.Render;

public class TextmateColoredRun : Run
{
    public IToken? Token { get; }

    public static readonly DependencyProperty ThemeColorsProperty = DependencyProperty.Register(
        nameof(ThemeColors), typeof(TextMateThemeColors), typeof(TextmateColoredRun),
        new FrameworkPropertyMetadata(null,
            new PropertyChangedCallback(ThemePropertyChangedCallback)));

    private static void ThemePropertyChangedCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TextmateColoredRun textmateRun && e.NewValue is TextMateThemeColors themeColors)
        {
            textmateRun.Color(themeColors);
        }
    }

    public TextMateThemeColors ThemeColors
    {
        get { return (TextMateThemeColors)GetValue(ThemeColorsProperty); }
        set { SetValue(ThemeColorsProperty, value); }
    }

    public TextmateColoredRun()
    {
    }

    public TextmateColoredRun(string text, IToken token) : base(text)
    {
        Token = token;
    }

    protected override void OnInitialized(EventArgs e)
    {
        base.OnInitialized(e);
        this.Color(this.ThemeColors);
    }

    private void Color(TextMateThemeColors textMateThemeColors)
    {
        if (this.Token == null)
        {
            return;
        }

        var foreground = -1;
        // var background = -1;
        // var textMateThemeColors = this.ThemeColors;
        var themeColorsTheme = textMateThemeColors.Theme;
        var fontStyle = TextMateSharp.Themes.FontStyle.None;
        var rules = themeColorsTheme.Match(this.Token.Scopes);
        foreach (var themeRule in rules)
        {
            if (foreground == -1 && themeRule.foreground > 0)
                foreground = themeRule.foreground;

            /*if (background == -1 && themeRule.background > 0)
                background = themeRule.background;*/

            if (themeRule.fontStyle > 0)
                fontStyle |= themeRule.fontStyle;
        }

        // var backgroundColor = textMateThemeColors.GetBrush(background);
        var foregroundColor = textMateThemeColors.GetBrush(foreground);
        if (foregroundColor != null)
        {
            this.Foreground = foregroundColor;
        }
        else
        {
            this.ClearValue(ForegroundProperty);
        }

        /*if (backgroundColor != null)
        {
            this.Background = backgroundColor;
        }*/
        this.ClearValue(TextDecorationsProperty);
        this.ClearValue(FontWeightProperty);
        this.ClearValue(FontStyleProperty);
        if (fontStyle != TextMateSharp.Themes.FontStyle.NotSet && fontStyle != TextMateSharp.Themes.FontStyle.None)
        {
            if (fontStyle.HasFlag(TextMateSharp.Themes.FontStyle.Italic))
            {
                this.FontStyle = FontStyles.Italic;
            }

            if (fontStyle.HasFlag(TextMateSharp.Themes.FontStyle.Bold))
            {
                this.FontWeight = FontWeights.Bold;
            }

            if (fontStyle.HasFlag(TextMateSharp.Themes.FontStyle.Underline))
            {
                this.TextDecorations = System.Windows.TextDecorations.Underline;
            }

            if (fontStyle.HasFlag(TextMateSharp.Themes.FontStyle.Strikethrough))
            {
                this.TextDecorations = System.Windows.TextDecorations.Strikethrough;
            }
        }
    }
}