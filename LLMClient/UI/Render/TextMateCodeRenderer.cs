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
using Block = System.Windows.Documents.Block;
using CodeBlockRenderer = Markdig.Renderers.Wpf.CodeBlockRenderer;
using Path = System.IO.Path;

namespace LLMClient.UI.Render;

public class TextMateCodeRenderer : CodeBlockRenderer
{
    public static ComponentResourceKey TokenStyleKey { get; } =
        new ComponentResourceKey(typeof(TextMateCodeRenderer), (object)nameof(TokenStyleKey));

    public static ComponentResourceKey CodeBlockHeaderStyleKey { get; } =
        new ComponentResourceKey(typeof(TextMateCodeRenderer), (object)nameof(CodeBlockHeaderStyleKey));

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

    protected override void Write(WpfRenderer renderer, CodeBlock codeBlock)
    {
        #region header

        var blockUiContainer = new BlockUIContainer();
        var contentControl = new ContentControl();
        contentControl.SetResourceReference(FrameworkElement.StyleProperty, CodeBlockHeaderStyleKey);
        ((IAddChild)blockUiContainer).AddChild(contentControl);
        renderer.Push(blockUiContainer);
        renderer.Pop();

        #endregion

        var root = new Section();
        renderer.Push(root);
        var codeContext = CreateCodeContext(codeBlock, root, renderer);
        contentControl.Content = codeContext;
        renderer.Pop();
        renderer.Pop();
    }

    private CodeViewModel CreateCodeContext(LeafBlock block, Section root, WpfRenderer renderer)
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

        return new CodeViewModel(root, renderer, block.Lines, extension, name, grammar);
    }

    public static Table CreateTable(Block left, Block right)
    {
        // 构造表格
        var table = new Table { CellSpacing = 4 };
        // 两列
        table.Columns.Add(new TableColumn() { Width = new GridLength(1, GridUnitType.Star) });
        table.Columns.Add(new TableColumn() { Width = new GridLength(1, GridUnitType.Star) });
        // 一行
        var rowGroup = new TableRowGroup();
        var row = new TableRow();
        rowGroup.Rows.Add(row);
        table.RowGroups.Add(rowGroup);
        // 单元格 1：图片
        var cell1 = new TableCell(right);
        var cell2 = new TableCell(left);
        row.Cells.Add(cell1);
        row.Cells.Add(cell2);
        return table;
    }

    public static BlockUIContainer CreateHtmlView(string codeString)
    {
        var htmlViewContext = new HtmlViewContext() { HtmlContent = codeString };
        var blockUiContainer = new BlockUIContainer();
        var contentControl = new ContentControl();
        contentControl.SetResourceReference(FrameworkElement.StyleProperty, HtmlViewContext.HtmlViewContextKey);
        ((IAddChild)blockUiContainer).AddChild(contentControl);
        contentControl.Content = htmlViewContext;
        return blockUiContainer;
    }
}