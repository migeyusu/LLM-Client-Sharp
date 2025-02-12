using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using Markdig.Helpers;
using Markdig.Renderers;
using Markdig.Renderers.Wpf;
using Markdig.Syntax;
using Markdig.Wpf;
using Microsoft.Xaml.Behaviors.Core;
using TextMateSharp.Grammars;
using TextMateSharp.Internal.Grammars;
using TextMateSharp.Registry;
using TextMateSharp.Themes;
using FontStyle = TextMateSharp.Themes.FontStyle;

namespace LLMClient.Render;

public class CodeContext
{
    public CodeContext(string? extension, StringLineGroup code)
    {
        Extension = extension;
        CodeGroup = code;
    }

    public string? Extension { get; set; }
    public StringLineGroup CodeGroup { get; set; }
    public ICommand CopyCommand => new ActionCommand(o => { Clipboard.SetText(CodeGroup.ToString()); });
}

public class TextMateCodeRenderer : CodeBlockRenderer
{
    public static ComponentResourceKey TokenStyleKey { get; } =
        new ComponentResourceKey(typeof(Styles), (object)nameof(TokenStyleKey));

    private static readonly RegistryOptions Options;

    static Dictionary<ThemeName, TextMateThemeColors> Themes = new Dictionary<ThemeName, TextMateThemeColors>();

    public static TextMateThemeColors GetTheme(ThemeName themeName)
    {
        if (!Themes.TryGetValue(themeName, out var theme))
        {
            theme = new TextMateThemeColors(Theme.CreateFromRawTheme(
                Options.LoadTheme(themeName), Options));
            Themes.Add(themeName, theme);
        }

        return theme;
    }

    static TextMateCodeRenderer()
    {
        Options = new RegistryOptions(ThemeName.Light);
    }

    private readonly Registry _registry;

    public TextMateCodeRenderer()
    {
        Options.LoadFromLocalDir("Grammars");
        _registry = new Registry(Options);
        
    }


    protected override void Write(WpfRenderer renderer, CodeBlock obj)
    {
        var blockUiContainer = new BlockUIContainer();
        var contentControl = new ContentControl();
        contentControl.SetResourceReference(FrameworkElement.StyleProperty,
            MarkdownStyles.CodeBlockHeaderStyleKey);
        ((IAddChild)blockUiContainer).AddChild(contentControl);
        renderer.Push(blockUiContainer);
        renderer.Pop();
        var paragraph = new Paragraph();
        paragraph.BeginInit();
        paragraph.SetResourceReference(FrameworkContentElement.StyleProperty, Styles.CodeBlockStyleKey);
        renderer.Push(paragraph);
        var written = false;
        if (obj is FencedCodeBlock fencedCodeBlock)
        {
            var extension = fencedCodeBlock.Info;
            contentControl.Content = new CodeContext(extension, fencedCodeBlock.Lines);
            if (extension != null)
            {
                var scope = Options.GetScopeByLanguageId(extension);
                if (scope == null)
                {
                    scope = Options.GetScopeByExtension("." + extension);
                }

                var grammar = _registry.LoadGrammar(scope);
                if (grammar != null)
                {
                    written = true;
                    Tokenize(renderer, fencedCodeBlock, grammar);
                    // CodeHighlight.TextMateColor(renderer, fencedCodeBlock, grammar, _theme);
                }
            }
        }

        if (!written)
        {
            renderer.WriteLeafRawLines(obj);
        }
        paragraph.EndInit();
        renderer.Pop();
    }

    private static void Tokenize(WpfRenderer renderer, FencedCodeBlock block, IGrammar grammar)
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
                var text = line.SubstringAtIndexes(startIndex, endIndex);
                var coloredRun = new TextmateColoredRun(text, token);
                coloredRun.SetResourceReference(FrameworkContentElement.StyleProperty,
                    TextMateCodeRenderer.TokenStyleKey);
                renderer.WriteInline(coloredRun);
            }

            renderer.WriteInline(new LineBreak());
        }
    }
}

public class TextmateColoredRun : Run
{
    public IToken Token { get; }

    public static readonly DependencyProperty ThemeColorsProperty = DependencyProperty.Register(
        nameof(ThemeColors), typeof(TextMateThemeColors), typeof(TextmateColoredRun),
        new FrameworkPropertyMetadata(TextMateCodeRenderer.GetTheme(ThemeName.Light),
            new PropertyChangedCallback(ThemePropertyChangedCallback)));

    private static void ThemePropertyChangedCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TextmateColoredRun textmateRun)
        {
            // textmateRun.Color();
        }
    }

    public TextMateThemeColors ThemeColors
    {
        get { return (TextMateThemeColors)GetValue(ThemeColorsProperty); }
        set { SetValue(ThemeColorsProperty, value); }
    }

    public TextmateColoredRun(IToken token) : base()
    {
        Token = token;
    }
    
    public TextmateColoredRun(string text, IToken token) : base(text)
    {
        Token = token;
        UITheme.OnThemeChanged+= UIThemeOnOnThemeChanged;
    }

    private void UIThemeOnOnThemeChanged(TextMateThemeColors obj)
    {
        Color(obj);
    }

    ~TextmateColoredRun()
    {
        UITheme.OnThemeChanged -= UIThemeOnOnThemeChanged;
    }
        
    protected override void OnInitialized(EventArgs e)
    {
        Color(UITheme.ThemeName);
        base.OnInitialized(e);
    }


    private void Color(TextMateThemeColors textMateThemeColors)
    {
        var foreground = -1;
        // var background = -1;
        // var textMateThemeColors = this.ThemeColors;
        var themeColorsTheme = textMateThemeColors.Theme;
        var fontStyle = TextMateSharp.Themes.FontStyle.NotSet;
        var rules = themeColorsTheme.Match(this.Token.Scopes);
        foreach (var themeRule in rules)
        {
            if (foreground == -1 && themeRule.foreground > 0)
                foreground = themeRule.foreground;

            /*if (background == -1 && themeRule.background > 0)
                background = themeRule.background;*/

            if (fontStyle == TextMateSharp.Themes.FontStyle.NotSet && themeRule.fontStyle > 0)
                fontStyle = themeRule.fontStyle;
        }
        
        // var backgroundColor = textMateThemeColors.GetBrush(background);
        var foregroundColor = textMateThemeColors.GetBrush(foreground);
        if (foregroundColor != null)
        {
            this.Foreground = foregroundColor;
        }

        /*if (backgroundColor != null)
        {
            this.Background = backgroundColor;
        }*/

        switch (fontStyle)
        {
            case TextMateSharp.Themes.FontStyle.Italic:
                this.FontStyle = FontStyles.Italic;
                break;
            case TextMateSharp.Themes.FontStyle.Bold:
                this.FontWeight = FontWeights.Bold;
                break;
            case TextMateSharp.Themes.FontStyle.Underline:
                this.TextDecorations = System.Windows.TextDecorations.Underline;
                break;
            case TextMateSharp.Themes.FontStyle.Strikethrough:
                this.TextDecorations = System.Windows.TextDecorations.Strikethrough;
                break;
            case TextMateSharp.Themes.FontStyle.NotSet:
            case TextMateSharp.Themes.FontStyle.None:
            default:
                break;
        }
    }

    private static void ColorInline(Run run, int foreground, int background,
        FontStyle fontStyle,
        TextMateThemeColors theme)
    {
    }
}

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

/*var code = content.TrimStart("cpp".ToCharArray()).TrimStart('\n');
        // 创建 ANTLR 输入流
        AntlrInputStream inputStream = new AntlrInputStream(codetest);

        // 使用生成的 CSharpLexer 分析词法
        CPP14Lexer lexer = new CPP14Lexer(inputStream);
        CommonTokenStream tokenStream = new CommonTokenStream(lexer);

        // 遍历所有 Token
        tokenStream.Fill();
        var enumerable = tokenStream.GetTokens();
        var enumerableCount = enumerable.Count;
        foreach (var token in enumerable)
        {
            // 根据 Token 类型输出不同颜色
            /*string highlighted = HighlightToken(token);
            Console.Write(highlighted);* /
        }*/
/*string HighlightToken(IToken token)
{
    // 获取 Token 类型
    string tokenText = token.Text;
    int tokenType = token.Type;
    Console.WriteLine(tokenType);
    // 根据 Token 类型添加样式
    switch (tokenType)
    {
        case CPP14Lexer.Class: // 这是 C# 的关键字
            return $"<span style='color: blue;'>{tokenText}</span>";
        case CPP14Lexer.And: // 字符串
            return $"<span style='color: green;'>{tokenText}</span>";
        case CPP14Lexer.Catch: // 注释
            return $"<span style='color: grey;'>{tokenText}</span>";
        default: // 其他类型，默认不加样式
            return tokenText;
    }
}*/