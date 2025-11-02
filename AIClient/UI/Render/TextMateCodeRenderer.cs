using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using Markdig.Renderers;
using Markdig.Syntax;
using Markdig.Wpf;
using TextMateSharp.Grammars;
using TextMateSharp.Registry;
using TextMateSharp.Themes;
using CodeBlockRenderer = Markdig.Renderers.Wpf.CodeBlockRenderer;
using Path = System.IO.Path;
using TextBlock = System.Windows.Controls.TextBlock;

namespace LLMClient.UI.Render;

public class TextMateCodeRenderer : CodeBlockRenderer
{
    public static ComponentResourceKey TokenStyleKey { get; } =
        new ComponentResourceKey(typeof(TextMateCodeRenderer), (object)nameof(TokenStyleKey));

    public static ComponentResourceKey CodeBlockHeaderStyleKey { get; } =
        new ComponentResourceKey(typeof(TextMateCodeRenderer), (object)nameof(CodeBlockHeaderStyleKey));

    public static ComponentResourceKey CodeBlockGroupBoxStyleKey { get; } =
        new ComponentResourceKey(typeof(TextMateCodeRenderer), (object)nameof(CodeBlockGroupBoxStyleKey));

    public static ComponentResourceKey CodeBlockTextStyleKey { get; } =
        new ComponentResourceKey(typeof(TextMateCodeRenderer), (object)nameof(CodeBlockTextStyleKey));

    private static readonly RegistryOptions Options;

    static readonly Dictionary<ThemeName, TextMateThemeColors>
        Themes = new Dictionary<ThemeName, TextMateThemeColors>();

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
        Options.LoadFromLocalDir(@"Resources\Grammars");
        _registry = new Registry(Options);
    }

    protected override void Write(WpfRenderer renderer, CodeBlock obj)
    {
        var table = new Table();
        table.Columns.Add(new TableColumn() { Width = new GridLength(1, GridUnitType.Star) });
        table.Columns.Add(new TableColumn() { Width = new GridLength(1, GridUnitType.Star) });
        var tableCell = new TableCell();
        var paragraph = new Paragraph(){TextAlignment = TextAlignment.Left};
        tableCell.Blocks.Add(paragraph);
        var rightCell = new TableCell();
        rightCell.Blocks.Add(new BlockUIContainer(new Button(){Content = "asdfasdfasdfa"}));
        TableRow tableRow = new TableRow();
        tableRow.Cells.Add(tableCell);
        tableRow.Cells.Add(rightCell);
        TableRowGroup tableRowGroup = new TableRowGroup();
        tableRowGroup.Rows.Add(tableRow);
        table.RowGroups.Add(tableRowGroup);
        renderer.Push(table);
        renderer.Pop();
        /*var blockUiContainer = new BlockUIContainer();
        var codeBlockContainer = new HeaderedContentControl();
        codeBlockContainer.SetResourceReference(FrameworkElement.StyleProperty, CodeBlockGroupBoxStyleKey);
        ((IAddChild)blockUiContainer).AddChild(codeBlockContainer);
        renderer.Push(blockUiContainer);
        renderer.Pop();*/
        /*var textBlock = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Left,
            Padding = new Thickness(5, 10, 5, 10)
        };
        textBlock.SetResourceReference(FrameworkElement.StyleProperty, CodeBlockTextStyleKey);
        codeBlockContainer.Content = textBlock;*/
        var written = false;
        if (obj is FencedCodeBlock fencedCodeBlock)
        {
            var name = fencedCodeBlock.Info;
            var codeContext = new CodeContext(name, fencedCodeBlock.Lines);
            // codeBlockContainer.Header = codeContext;
            if (name != null)
            {
                var scope = Options.GetScopeByLanguageId(name) ?? Options.GetScopeByExtension("." + name);
                if (!string.IsNullOrEmpty(scope))
                {
                    codeContext.Extension = Path.GetExtension(scope);
                }

                var grammar = _registry.LoadGrammar(scope);
                if (grammar != null)
                {
                    written = true;
                    Tokenize(paragraph, fencedCodeBlock, grammar);
                }
            }
        }

        //todo:
        /*if (!written)
        {
            renderer.WriteLeafRawLines(obj);
        }*/

        /*#region header

        var blockUiContainer = new BlockUIContainer();
        var contentControl = new ContentControl();
        contentControl.SetResourceReference(FrameworkElement.StyleProperty, CodeBlockHeaderStyleKey);
        ((IAddChild)blockUiContainer).AddChild(contentControl);
        renderer.Push(blockUiContainer);
        renderer.Pop();

        #endregion

        var paragraph = new Paragraph();
        paragraph.BeginInit();
        paragraph.SetResourceReference(FrameworkContentElement.StyleProperty, Styles.CodeBlockStyleKey);
        renderer.Push(paragraph);
        var written = false;
        if (obj is FencedCodeBlock fencedCodeBlock)
        {
            var name = fencedCodeBlock.Info;
            var codeContext = new CodeContext(name, fencedCodeBlock.Lines);
            contentControl.Content = codeContext;
            if (name != null)
            {
                var scope = Options.GetScopeByLanguageId(name) ?? Options.GetScopeByExtension("." + name);
                if (!string.IsNullOrEmpty(scope))
                {
                    codeContext.Extension = Path.GetExtension(scope);
                }

                var grammar = _registry.LoadGrammar(scope);
                if (grammar != null)
                {
                    written = true;
                    Tokenize(renderer, fencedCodeBlock, grammar);
                }
            }
        }

        if (!written)
        {
            renderer.WriteLeafRawLines(obj);
        }

        paragraph.EndInit();
        renderer.Pop();*/
    }

    private static void Tokenize(IAddChild addChild, FencedCodeBlock block, IGrammar grammar)
    {
        IStateStack? ruleStack = null;
        var stringLineGroup = block.Lines;
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