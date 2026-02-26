using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using Markdig.Renderers;
using Markdig.Syntax;
using TextMateSharp.Grammars;
using TextMateSharp.Registry;
using TextMateSharp.Themes;
using CodeBlockRenderer = Markdig.Renderers.Wpf.CodeBlockRenderer;
using Path = System.IO.Path;

namespace LLMClient.Component.Render;

public class TextMateCodeRenderer : CodeBlockRenderer
{
    public static ComponentResourceKey TokenStyleKey { get; } =
        new(typeof(TextMateCodeRenderer), nameof(TokenStyleKey));

    public static ComponentResourceKey CodeBlockHeaderStyleKey { get; } =
        new(typeof(TextMateCodeRenderer), nameof(CodeBlockHeaderStyleKey));

    private class TextMateCodeRendererSettings
    {
        public RegistryOptions Options { get; }

        public Registry Registry { get; }

        private readonly Dictionary<ThemeName, TextMateThemeColors> _themes = new();

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

    protected override void Write(WpfRenderer renderer, CodeBlock codeBlock)
    {
        if (renderer is CustomMarkdownRenderer { EnableTextMateHighlighting: false })
        {
            base.Write(renderer, codeBlock);
            return;
        }

        #region header

        var blockUiContainer = new BlockUIContainer();
        var contentControl = new ContentControl();
        contentControl.SetResourceReference(FrameworkElement.StyleProperty, CodeBlockHeaderStyleKey);
        ((IAddChild)blockUiContainer).AddChild(contentControl);
        renderer.Push(blockUiContainer);
        renderer.Pop();

        #endregion

        var codeContext = CreateCodeContext(codeBlock, renderer);
        contentControl.Content = codeContext;
        renderer.Pop();
    }

    private CodeViewModel CreateCodeContext(LeafBlock block, WpfRenderer renderer)
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

        return new CodeViewModel(renderer, block.Lines, extension, name, grammar);
    }
}