using System.Diagnostics;
using System.Text;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Navigation;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Block = Markdig.Syntax.Block;
using Inline = System.Windows.Documents.Inline;

namespace LLMClient.Component.Render;

/// <summary>
/// Converts a Markdig <see cref="MarkdownDocument"/> AST into WPF <see cref="Inline"/>
/// elements that can be displayed inside a <see cref="System.Windows.Controls.TextBlock"/>.
/// <para>
/// Supports: paragraphs, headings (H1–H6), bold, italic, strikethrough, inserted text,
/// marked/highlighted text, inline code, fenced/indented code blocks, links, images (as
/// text), auto-links, lists (ordered, unordered, nested), blockquotes, thematic breaks,
/// and HTML entities.
/// </para>
/// <para>
/// Override <see cref="RenderBlock"/> or <see cref="RenderInline"/> to extend for
/// custom Markdig block/inline types.
/// </para>
/// </summary>
public class MarkdownInlineRenderer
{
    // ─── Shared resources ─────────────────────────────────────────────

    private static readonly Lazy<MarkdownPipeline> PipelineLazy = new(() =>
        new MarkdownPipelineBuilder().UseAdvancedExtensions().Build());

    /// <summary>The Markdig pipeline used for parsing.</summary>
    public static MarkdownPipeline Pipeline => PipelineLazy.Value;

    private static readonly FontFamily MonoFont = new("Consolas");

    private static readonly SolidColorBrush CodeBackgroundBrush = Freeze(new SolidColorBrush(Color.FromArgb(40, 128, 128, 128)));
    private static readonly SolidColorBrush QuoteForegroundBrush = Freeze(new SolidColorBrush(Colors.Gray));
    private static readonly SolidColorBrush RuleBrush = Freeze(new SolidColorBrush(Colors.DarkGray));
    private static readonly SolidColorBrush ImageBrush = Freeze(new SolidColorBrush(Colors.SteelBlue));
    private static readonly SolidColorBrush MarkBrush = Freeze(new SolidColorBrush(Color.FromArgb(60, 255, 255, 0)));

    private static SolidColorBrush Freeze(SolidColorBrush brush) { brush.Freeze(); return brush; }

    /// <summary>Font-size multipliers relative to <see cref="SystemFonts.MessageFontSize"/> for H1–H6.</summary>
    private static readonly double[] HeadingScales = [2.0, 1.5, 1.25, 1.1, 1.0, 0.875];

    // ─── Public API ───────────────────────────────────────────────────

    /// <summary>
    /// Parses <paramref name="markdown"/> and replaces all existing inlines in
    /// <paramref name="target"/> with the rendered result.
    /// </summary>
    public void Render(string markdown, InlineCollection target)
    {
        target.Clear();
        if (string.IsNullOrWhiteSpace(markdown)) return;

        try
        {
            var document = Markdown.Parse(markdown, Pipeline);
            RenderDocument(document, target);
        }
        catch (Exception ex)
        {
            Trace.TraceWarning($"MarkdownInlineRenderer parse error: {ex.Message}");
            target.Add(new Run(markdown));
        }
    }

    // ─── Document entry ───────────────────────────────────────────────

    private void RenderDocument(MarkdownDocument document, InlineCollection target)
    {
        RenderBlocks(document, target);
        TrimTrailingBreak(target);
    }

    // ─── Block rendering ──────────────────────────────────────────────

    private void RenderBlocks(ContainerBlock container, ICollection<Inline> target)
    {
        for (int i = 0; i < container.Count; i++)
        {
            RenderBlock(container[i], target);
        }
    }

    /// <summary>
    /// Renders a single Markdig <see cref="Block"/> into WPF <see cref="Inline"/> elements.
    /// Override to add support for custom block types.
    /// </summary>
    protected virtual void RenderBlock(Block block, ICollection<Inline> target)
    {
        switch (block)
        {
            case ParagraphBlock paragraph:
                RenderParagraph(paragraph, target);
                break;

            case HeadingBlock heading:
                RenderHeading(heading, target);
                break;

            case FencedCodeBlock fencedCode:
                RenderFencedCodeBlock(fencedCode, target);
                break;

            case CodeBlock code:
                RenderIndentedCodeBlock(code, target);
                break;

            case ListBlock list:
                RenderList(list, target, depth: 0);
                break;

            case QuoteBlock quote:
                RenderQuoteBlock(quote, target);
                break;

            case ThematicBreakBlock:
                RenderThematicBreak(target);
                break;

            case ContainerBlock container:
                // Generic container fallback (MarkdownDocument, custom extension blocks, etc.)
                RenderBlocks(container, target);
                break;

            case LeafBlock leaf:
                // Generic leaf fallback — emit raw text
                RenderLeafAsText(leaf, target);
                break;
        }
    }

    private void RenderParagraph(ParagraphBlock paragraph, ICollection<Inline> target)
    {
        AddInlines(paragraph.Inline, target);
        target.Add(new LineBreak());
    }

    private void RenderHeading(HeadingBlock heading, ICollection<Inline> target)
    {
        int level = Math.Clamp(heading.Level, 1, 6);
        double scale = HeadingScales[level - 1];

        var span = new Span
        {
            FontWeight = FontWeights.Bold,
            FontSize = SystemFonts.MessageFontSize * scale,
        };

        AddInlines(heading.Inline, span.Inlines);
        target.Add(span);
        target.Add(new LineBreak());
    }

    private void RenderFencedCodeBlock(FencedCodeBlock fencedCode, ICollection<Inline> target)
    {
        string code = ExtractLeafText(fencedCode);
        target.Add(CreateCodeRun(code));
        target.Add(new LineBreak());
    }

    private void RenderIndentedCodeBlock(CodeBlock codeBlock, ICollection<Inline> target)
    {
        string code = ExtractLeafText(codeBlock);
        target.Add(CreateCodeRun(code));
        target.Add(new LineBreak());
    }

    private void RenderList(ListBlock list, ICollection<Inline> target, int depth)
    {
        int ordinal = 1;
        if (list.IsOrdered && list.OrderedStart is not null)
            int.TryParse(list.OrderedStart, out ordinal);

        string indent = depth > 0 ? new string(' ', depth * 3) : string.Empty;

        foreach (var item in list)
        {
            if (item is not ListItemBlock listItem) continue;

            string bullet = list.IsOrdered
                ? $"{indent}{ordinal++}. "
                : $"{indent}• ";

            target.Add(new Run(bullet));

            for (int i = 0; i < listItem.Count; i++)
            {
                var child = listItem[i];
                switch (child)
                {
                    case ParagraphBlock para:
                        AddInlines(para.Inline, target);
                        break;

                    case ListBlock nested:
                        target.Add(new LineBreak());
                        RenderList(nested, target, depth + 1);
                        continue; // LineBreak already handled inside nested list

                    default:
                        RenderBlock(child, target);
                        continue;
                }
            }

            target.Add(new LineBreak());
        }
    }

    private void RenderQuoteBlock(QuoteBlock quote, ICollection<Inline> target)
    {
        var span = new Span { FontStyle = FontStyles.Italic };

        for (int i = 0; i < quote.Count; i++)
        {
            span.Inlines.Add(new Run("│ ") { Foreground = QuoteForegroundBrush, FontStyle = FontStyles.Normal });

            var child = quote[i];
            switch (child)
            {
                case ParagraphBlock para:
                    AddInlines(para.Inline, span.Inlines);
                    break;

                case QuoteBlock nested:
                {
                    // Nested blockquote — render recursively into a nested Span
                    var nestedSpan = new Span();
                    RenderQuoteBlock(nested, nestedSpan.Inlines);
                    span.Inlines.Add(nestedSpan);
                    continue; // The nested call handles its own breaks
                }

                default:
                    RenderBlock(child, span.Inlines);
                    continue;
            }

            if (i < quote.Count - 1)
                span.Inlines.Add(new LineBreak());
        }

        target.Add(span);
        target.Add(new LineBreak());
    }

    private static void RenderThematicBreak(ICollection<Inline> target)
    {
        target.Add(new Run("────────────────────────────────") { Foreground = RuleBrush });
        target.Add(new LineBreak());
    }

    private void RenderLeafAsText(LeafBlock leaf, ICollection<Inline> target)
    {
        // Prefer inline content if parsed
        if (leaf.Inline is not null)
        {
            AddInlines(leaf.Inline, target);
            target.Add(new LineBreak());
            return;
        }

        string text = ExtractLeafText(leaf);
        if (!string.IsNullOrEmpty(text))
        {
            target.Add(new Run(text));
            target.Add(new LineBreak());
        }
    }

    // ─── Inline rendering ─────────────────────────────────────────────

    private void AddInlines(ContainerInline? container, ICollection<Inline> target)
    {
        if (container is null) return;
        foreach (var mdInline in container)
            target.Add(RenderInline(mdInline));
    }

    /// <summary>
    /// Converts a single Markdig <see cref="Markdig.Syntax.Inlines.Inline"/> into a
    /// WPF <see cref="Inline"/>. Override to add support for custom inline types.
    /// </summary>
    protected virtual Inline RenderInline(Markdig.Syntax.Inlines.Inline mdInline)
    {
        return mdInline switch
        {
            LiteralInline literal => new Run(literal.Content.ToString()),
            EmphasisInline emphasis => RenderEmphasis(emphasis),
            CodeInline code => RenderCodeInline(code),
            LinkInline link => RenderLink(link),
            AutolinkInline autolink => RenderAutolink(autolink),
            LineBreakInline lb => lb.IsHard ? new LineBreak() : (Inline)new Run(" "),
            HtmlEntityInline entity => new Run(entity.Transcoded.ToString()),
            HtmlInline html => new Run(html.Tag),
            ContainerInline container => RenderGenericContainer(container),
            _ => new Run(mdInline.ToString() ?? string.Empty),
        };
    }

    private Inline RenderEmphasis(EmphasisInline emphasis)
    {
        var span = new Span();
        foreach (var child in emphasis)
            span.Inlines.Add(RenderInline(child));

        switch (emphasis.DelimiterChar)
        {
            case '*' or '_':
                if (emphasis.DelimiterCount >= 2)
                    span.FontWeight = FontWeights.Bold;
                else
                    span.FontStyle = FontStyles.Italic;
                break;

            case '~' when emphasis.DelimiterCount == 2:
                span.TextDecorations = TextDecorations.Strikethrough;
                break;

            case '+' when emphasis.DelimiterCount == 2:
                span.TextDecorations = TextDecorations.Underline;
                break;

            case '=' when emphasis.DelimiterCount == 2:
                span.Background = MarkBrush;
                break;
        }

        return span;
    }

    private static Inline RenderCodeInline(CodeInline code)
    {
        return new Run(code.Content)
        {
            FontFamily = MonoFont,
            Background = CodeBackgroundBrush,
        };
    }

    private Inline RenderLink(LinkInline link)
    {
        if (link.IsImage)
        {
            string alt = link.FirstChild is LiteralInline lit
                ? lit.Content.ToString()
                : "image";
            return new Run($"[🖼 {alt}]") { Foreground = ImageBrush };
        }

        try
        {
            var hyperlink = new Hyperlink
            {
                NavigateUri = new Uri(link.Url ?? "#", UriKind.RelativeOrAbsolute),
            };
            hyperlink.RequestNavigate += OnRequestNavigate;

            foreach (var child in link)
                hyperlink.Inlines.Add(RenderInline(child));

            if (hyperlink.Inlines.FirstInline is null)
                hyperlink.Inlines.Add(new Run(link.Url ?? "link"));

            return hyperlink;
        }
        catch
        {
            // Invalid URI — render as plain text
            var span = new Span();
            foreach (var child in link)
                span.Inlines.Add(RenderInline(child));
            return span.Inlines.FirstInline is null ? new Run(link.Url ?? string.Empty) : span;
        }
    }

    private static Inline RenderAutolink(AutolinkInline autolink)
    {
        try
        {
            string url = autolink.IsEmail ? $"mailto:{autolink.Url}" : autolink.Url;
            var hyperlink = new Hyperlink(new Run(autolink.Url))
            {
                NavigateUri = new Uri(url, UriKind.RelativeOrAbsolute),
            };
            hyperlink.RequestNavigate += OnRequestNavigate;
            return hyperlink;
        }
        catch
        {
            return new Run(autolink.Url);
        }
    }

    private Inline RenderGenericContainer(ContainerInline container)
    {
        var span = new Span();
        foreach (var child in container)
            span.Inlines.Add(RenderInline(child));
        return span;
    }

    // ─── Helpers ──────────────────────────────────────────────────────

    /// <summary>Thread-local reusable StringBuilder to reduce allocations in ExtractLeafText.</summary>
    [ThreadStatic] private static StringBuilder? t_leafBuilder;

    private static Run CreateCodeRun(string code)
    {
        // Trim trailing newline that Markdig appends to code block content
        if (code.Length > 0 && code[^1] == '\n')
            code = code[..^1];

        return new Run(code)
        {
            FontFamily = MonoFont,
            Background = CodeBackgroundBrush,
        };
    }

    private static string ExtractLeafText(LeafBlock leaf)
    {
        var lines = leaf.Lines;
        if (lines.Count == 0) return string.Empty;

        if (lines.Count == 1)
            return lines.Lines[0].Slice.ToString();

        var sb = t_leafBuilder ??= new StringBuilder(256);
        sb.Clear();
        for (int i = 0; i < lines.Count; i++)
        {
            if (i > 0) sb.Append('\n');
            sb.Append(lines.Lines[i].Slice.ToString());
        }

        return sb.ToString();
    }

    private static void TrimTrailingBreak(InlineCollection inlines)
    {
        if (inlines.LastInline is LineBreak trailing)
            inlines.Remove(trailing);
    }

    private static void OnRequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Trace.TraceWarning($"Failed to navigate: {ex.Message}");
        }

        e.Handled = true;
    }
}

