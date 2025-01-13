using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using Azure.Core;
using Markdig.Renderers;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Markdig.Wpf;
using TextMateSharp.Grammars;
using TextMateSharp.Registry;
using TextMateSharp.Themes;
using FontStyle = TextMateSharp.Themes.FontStyle;
using IToken = TextMateSharp.Grammars.IToken;

namespace LLMClient;

public class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        App app = new App();
        app.InitializeComponent();
        app.Run();
    }
    
    public static void TextMateColor(WpfRenderer renderer, FencedCodeBlock block, IGrammar grammar, Theme theme)
    {
        IStateStack? ruleStack = null;
        if (block.Lines.Lines == null)
        {
            return;
        }

        foreach (var blockLine in block.Lines.Lines)
        {
            var line = blockLine.Slice.ToString();
            if (blockLine.Slice.Length == 0 || string.IsNullOrEmpty(line))
            {
                renderer.WriteInline(new LineBreak());
                continue;
            }

            var result = grammar.TokenizeLine(line, ruleStack, TimeSpan.MaxValue);
            ruleStack = result.RuleStack;
            foreach (var token in result.Tokens)
            {
                var lineLength = line.Length;
                var tokenStartIndex = token.StartIndex;

                var startIndex = (tokenStartIndex > lineLength) ? lineLength : tokenStartIndex;
                var endIndex = (token.EndIndex > lineLength) ? lineLength : token.EndIndex;
                var foreground = -1;
                var background = -1;
                var fontStyle = FontStyle.NotSet;
                foreach (var themeRule in theme.Match(token.Scopes))
                {
                    if (foreground == -1 && themeRule.foreground > 0)
                        foreground = themeRule.foreground;

                    if (background == -1 && themeRule.background > 0)
                        background = themeRule.background;

                    if (fontStyle == FontStyle.NotSet && themeRule.fontStyle > 0)
                        fontStyle = themeRule.fontStyle;
                }

                var writeToken = GetColoredInline(line.SubstringAtIndexes(startIndex, endIndex), foreground, background,
                    fontStyle, theme);
                renderer.WriteInline(writeToken);
            }

            renderer.WriteInline(new LineBreak());
        }

        /*var colorDictionary = theme.GetGuiColorDictionary();
        if (colorDictionary is { Count: > 0 })
        {
            Console.WriteLine("Gui Control Colors");
            foreach (var kvp in colorDictionary)
            {
                Console.WriteLine($"  {kvp.Key}, {kvp.Value}");
            }
        }*/
    }

    static Color? GetColor(int colorId, Theme theme)
    {
        if (colorId == -1)
            return null;
        return HexToColor(theme.GetColor(colorId));
    }

    static Color HexToColor(string hexString)
    {
        //replace # occurences
        if (hexString.IndexOf('#') != -1)
            hexString = hexString.Replace("#", "");
        var r = byte.Parse(hexString.Substring(0, 2), NumberStyles.AllowHexSpecifier);
        var g = byte.Parse(hexString.Substring(2, 2), NumberStyles.AllowHexSpecifier);
        var b = byte.Parse(hexString.Substring(4, 2), NumberStyles.AllowHexSpecifier);
        return Color.FromRgb(r, g, b);
    }

    static SolidColorBrush defaultBrush = Brushes.Black;

    private static System.Windows.Documents.Inline GetColoredInline(string text, int foreground, int background,
        FontStyle fontStyle,
        Theme theme)
    {
        var run = new Run(text);
        if (foreground == -1)
        {
            run.Foreground = defaultBrush;
            return run;
        }

        var backgroundColor = GetColor(background, theme);
        var foregroundColor = GetColor(foreground, theme);
        if (foregroundColor != null)
        {
            run.Foreground = new SolidColorBrush(foregroundColor.Value);
        }

        if (backgroundColor != null)
        {
            run.Background = new SolidColorBrush(backgroundColor.Value);
        }

        switch (fontStyle)
        {
            case FontStyle.Italic:
                run.FontStyle = FontStyles.Italic;
                break;
            case FontStyle.Bold:
                run.FontWeight = FontWeights.Bold;
                break;
            case FontStyle.Underline:
                run.TextDecorations = TextDecorations.Underline;
                break;
            case FontStyle.Strikethrough:
                run.TextDecorations = TextDecorations.Strikethrough;
                break;
            case FontStyle.NotSet:
            case FontStyle.None:
            default:
                break;
        }

        return run;
    }
}

internal static class StringExtensions
{
    internal static string SubstringAtIndexes(this string str, int startIndex, int endIndex)
    {
        return str.Substring(startIndex, endIndex - startIndex);
    }
}