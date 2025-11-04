using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using Markdig.Helpers;
using Markdig.Renderers;
using Markdig.Syntax;
using Markdig.Wpf;
using TextMateSharp.Grammars;
using TextMateSharp.Registry;
using TextMateSharp.Themes;
using CodeBlockRenderer = Markdig.Renderers.Wpf.CodeBlockRenderer;
using Path = System.IO.Path;

namespace LLMClient.UI.Render;

public class TextMateCodeRenderer : CodeBlockRenderer
{
    public static ComponentResourceKey TokenStyleKey { get; } =
        new ComponentResourceKey(typeof(TextMateCodeRenderer), (object)nameof(TokenStyleKey));

    public static ComponentResourceKey CodeBlockGroupBoxStyleKey { get; } =
        new ComponentResourceKey(typeof(TextMateCodeRenderer), (object)nameof(CodeBlockGroupBoxStyleKey));

    private class TextMateCodeRendererSettings
    {
        public RegistryOptions Options { get; }

        public Registry Registry { get; }

        private readonly Dictionary<ThemeName, TextMateThemeColors>
            _themes = new Dictionary<ThemeName, TextMateThemeColors>();

        public TextMateCodeRendererSettings()
        {
            Options = new RegistryOptions(ThemeName.Light);
            Options.LoadFromLocalDir(@"Resources\Grammars");
            Registry = new Registry(Options);
            GetOrAddTheme(ThemeName.LightPlus);
            GetOrAddTheme(ThemeName.DarkPlus);
        }

        public TextMateThemeColors GetOrAddTheme(ThemeName themeName)
        {
            if (!_themes.TryGetValue(themeName, out var theme))
            {
                theme = new TextMateThemeColors(Theme.CreateFromRawTheme(
                    Options.LoadTheme(themeName), Options));
                _themes.Add(themeName, theme);
            }

            return theme;
        }
    }

    private const string ThemeColorResourceKey = "CodeBlock.TextMateSharp.Theme";

    public static void UpdateResource(ThemeName themeName)
    {
        var application = Application.Current;
        if (application == null)
        {
            return;
        }

        var sourceDictionary = application.Resources;
        sourceDictionary[ThemeColorResourceKey] = Settings.GetOrAddTheme(themeName);
    }

    private static TextMateCodeRendererSettings Settings =>
        _settings ??= new TextMateCodeRendererSettings();

    private static TextMateCodeRendererSettings? _settings = null;

    public static Task InitializeAsync()
    {
        return Task.Run(() => { _settings = new TextMateCodeRendererSettings(); });
    }

    protected override void Write(WpfRenderer renderer, CodeBlock obj)
    {
        var blockUiContainer = new BlockUIContainer();
        var codeBlockContainer = new HeaderedContentControl();
        codeBlockContainer.SetResourceReference(FrameworkElement.StyleProperty, CodeBlockGroupBoxStyleKey);
        ((IAddChild)blockUiContainer).AddChild(codeBlockContainer);
        renderer.Push(blockUiContainer);
        renderer.Pop();
        var codeContext = CreateCodeContext(obj);
        codeBlockContainer.Header = codeContext;
        codeBlockContainer.Content = codeContext;
    }

    private CodeContext CreateCodeContext(LeafBlock block)
    {
        string? extension = null;
        IGrammar? grammar = null;
        string? name = null;
        if (block is FencedCodeBlock fencedCodeBlock)
        {
            name = fencedCodeBlock.Info;
            if (name != null)
            {
                var rendererSettings = Settings;
                var options = rendererSettings.Options;
                var scope = options.GetScopeByLanguageId(name) ?? options.GetScopeByExtension("." + name);
                if (!string.IsNullOrEmpty(scope))
                {
                    extension = Path.GetExtension(scope);
                }

                grammar = rendererSettings.Registry.LoadGrammar(scope);
            }
        }

        return new CodeContext(block.Lines, extension, name, grammar);
    }

    public static FlowDocument Render(CodeContext codeContext)
    {
        var flowDocument = new FlowDocument();
        var wpfRenderer = new WpfRenderer(flowDocument);
        var paragraph = new Paragraph();
        paragraph.BeginInit();
        paragraph.SetResourceReference(FrameworkContentElement.StyleProperty, Styles.CodeBlockStyleKey);
        wpfRenderer.Push(paragraph);
        var grammar = codeContext.Grammar;
        if (grammar != null)
        {
            Tokenize(paragraph, codeContext.CodeGroup, grammar);
        }
        else
        {
            wpfRenderer.WriteRawLines(codeContext.CodeGroup);
        }

        paragraph.EndInit();
        wpfRenderer.Pop();
        return flowDocument;
    }


    private static void Tokenize(IAddChild addChild, StringLineGroup stringLineGroup, IGrammar grammar)
    {
        IStateStack? ruleStack = null;
        if (stringLineGroup.Lines == null)
        {
            return;
        }

        for (var index = 0; index < stringLineGroup.Count; index++)
        {
            var blockLine = stringLineGroup.Lines[index];
            var line = blockLine.Slice.ToString();
            if (blockLine.Slice.Length == 0 || string.IsNullOrEmpty(line))
            {
                addChild.AddChild(new LineBreak());
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
                addChild.AddChild(coloredRun);
            }

            addChild.AddChild(new LineBreak());
        }
    }
}