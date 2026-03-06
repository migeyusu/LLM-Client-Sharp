using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using ICSharpCode.AvalonEdit;
using Markdig.Renderers;
using Markdig.Renderers.Html;
using Markdig.Syntax;
using TextMateSharp.Grammars;
using TextMateSharp.Registry;
using TextMateSharp.Themes;
using CodeBlockRenderer = Markdig.Renderers.Wpf.CodeBlockRenderer;
using Path = System.IO.Path;

namespace LLMClient.Component.Render;

public class TextMateCodeRenderer : CodeBlockRenderer
{
    public static ComponentResourceKey CodeTokenStyleKey { get; } =
        new(typeof(TextMateCodeRenderer), nameof(CodeTokenStyleKey));

    public static ComponentResourceKey DisplayCodeBlockHeaderStyleKey { get; } =
        new(typeof(TextMateCodeRenderer), nameof(DisplayCodeBlockHeaderStyleKey));

    public static ComponentResourceKey EditCodeBlockStyleKey { get; } =
        new(typeof(TextMateCodeRenderer), nameof(EditCodeBlockStyleKey));


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
        if (renderer is not CustomMarkdownRenderer customMarkdownRenderer ||
            !customMarkdownRenderer.EnableTextMateHighlighting)
        {
            base.Write(renderer, codeBlock);
            return;
        }

        if (customMarkdownRenderer.EditMode)
        {
            var blockUiContainer = new BlockUIContainer();
            var contentControl = new ContentControl();
            contentControl.SetResourceReference(FrameworkElement.StyleProperty, EditCodeBlockStyleKey);
            var codeContext = CreateEditableCodeContext(codeBlock);
            contentControl.Content = codeContext;
            ((IAddChild)blockUiContainer).AddChild(contentControl);
            renderer.Push(blockUiContainer);
            renderer.Pop();
        }
        else
        {
            //渲染高亮后的只读代码块

            #region header

            var blockUiContainer = new BlockUIContainer();
            var contentControl = new ContentControl();
            contentControl.SetResourceReference(FrameworkElement.StyleProperty, DisplayCodeBlockHeaderStyleKey);
            ((IAddChild)blockUiContainer).AddChild(contentControl);
            renderer.Push(blockUiContainer);
            renderer.Pop();

            #endregion

            var codeContext = CreateReadOnlyCodeContext(codeBlock, renderer);
            contentControl.Content = codeContext;
        }
    }

    private static EditableCodeViewModel CreateEditableCodeContext(LeafBlock block)
    {
        string? extension = null;
        string? name = null;
        if (block is FencedCodeBlock fencedCodeBlock)
        {
            //HtmlAttributes htmlAttributes = fencedCodeBlock.GetAttributes();
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
            }
        }

        return new EditableCodeViewModel(block.Lines, extension, name);
    }

    private static DisplayCodeViewModel CreateReadOnlyCodeContext(LeafBlock block, WpfRenderer renderer)
    {
        string? extension = null;
        IGrammar? grammar = null;
        string? name = null;
        if (block is FencedCodeBlock fencedCodeBlock)
        {
            //HtmlAttributes htmlAttributes = fencedCodeBlock.GetAttributes();
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

        return new DisplayCodeViewModel(renderer, block.Lines, extension, name, grammar);
    }
}