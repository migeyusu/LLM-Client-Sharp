using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using LLMClient.Component.Render;

namespace LLMClient.Test;

public class MarkdownInlineRendererTests
{
    private readonly MarkdownInlineRenderer _renderer = new();

    /// <summary>
    /// Helper: render markdown and run assertions on an STA thread.
    /// All WPF object access must happen inside the STA callback.
    /// </summary>
    private void RenderAndAssert(string markdown, Action<List<Inline>> assertions)
    {
        TestFixture.RunInStaThread(() =>
        {
            var tb = new TextBlock();
            _renderer.Render(markdown, tb.Inlines);
            var inlines = tb.Inlines.ToList();
            assertions(inlines);
        });
    }

    // ─── Plain text ───────────────────────────────────────────────────

    [Fact]
    public void PlainText_RendersAsRun()
    {
        RenderAndAssert("Hello world", inlines =>
        {
            Assert.Single(inlines);
            Assert.IsType<Run>(inlines[0]);
            Assert.Equal("Hello world", ((Run)inlines[0]).Text);
        });
    }

    // ─── Bold ─────────────────────────────────────────────────────────

    [Fact]
    public void Bold_RendersWithBoldWeight()
    {
        RenderAndAssert("**bold**", inlines =>
        {
            var span = Assert.IsType<Span>(inlines[0]);
            Assert.Equal(FontWeights.Bold, span.FontWeight);
            var inner = span.Inlines.FirstInline;
            Assert.IsType<Run>(inner);
            Assert.Equal("bold", ((Run)inner!).Text);
        });
    }

    // ─── Italic ───────────────────────────────────────────────────────

    [Fact]
    public void Italic_RendersWithItalicStyle()
    {
        RenderAndAssert("*italic*", inlines =>
        {
            var span = Assert.IsType<Span>(inlines[0]);
            Assert.Equal(FontStyles.Italic, span.FontStyle);
            Assert.Equal("italic", ((Run)span.Inlines.FirstInline!).Text);
        });
    }

    // ─── Bold + Italic ────────────────────────────────────────────────

    [Fact]
    public void BoldItalic_NestsCorrectly()
    {
        RenderAndAssert("***both***", inlines =>
        {
            var outer = Assert.IsType<Span>(inlines[0]);
            var isBoldOuter = outer.FontWeight == FontWeights.Bold;
            var isItalicOuter = outer.FontStyle == FontStyles.Italic;
            Assert.True(isBoldOuter || isItalicOuter);
            var inner = Assert.IsType<Span>(outer.Inlines.FirstInline);
            var isBoldInner = inner.FontWeight == FontWeights.Bold;
            var isItalicInner = inner.FontStyle == FontStyles.Italic;
            // One should be bold, the other italic
            Assert.NotEqual(isBoldOuter, isBoldInner);
        });
    }

    // ─── Strikethrough ────────────────────────────────────────────────

    [Fact]
    public void Strikethrough_RendersWithStrikethrough()
    {
        RenderAndAssert("~~strike~~", inlines =>
        {
            var span = Assert.IsType<Span>(inlines[0]);
            Assert.NotNull(span.TextDecorations);
            Assert.Contains(span.TextDecorations, td => td == TextDecorations.Strikethrough[0]);
        });
    }

    // ─── Inline code ──────────────────────────────────────────────────

    [Fact]
    public void InlineCode_RendersWithMonoFontAndBackground()
    {
        RenderAndAssert("`code`", inlines =>
        {
            var run = Assert.IsType<Run>(inlines[0]);
            Assert.Equal("code", run.Text);
            Assert.Equal(new FontFamily("Consolas").Source, run.FontFamily.Source);
            Assert.NotNull(run.Background);
        });
    }

    // ─── Code block ───────────────────────────────────────────────────

    [Fact]
    public void FencedCodeBlock_RendersAsMonoRunWithBackground()
    {
        RenderAndAssert("```\nvar x = 1;\n```", inlines =>
        {
            var codeRun = inlines.OfType<Run>().First();
            Assert.Contains("var x = 1;", codeRun.Text);
            Assert.Equal(new FontFamily("Consolas").Source, codeRun.FontFamily.Source);
        });
    }

    // ─── Heading ──────────────────────────────────────────────────────

    [Fact]
    public void Heading_RendersAsBoldSpan()
    {
        RenderAndAssert("# Title", inlines =>
        {
            var span = Assert.IsType<Span>(inlines[0]);
            Assert.Equal(FontWeights.Bold, span.FontWeight);
            Assert.True(span.FontSize > SystemFonts.MessageFontSize);
            Assert.Equal("Title", ((Run)span.Inlines.FirstInline!).Text);
        });
    }

    [Fact]
    public void Heading_H2SmallerThanH1()
    {
        TestFixture.RunInStaThread(() =>
        {
            var tb1 = new TextBlock();
            _renderer.Render("# H1", tb1.Inlines);
            var h1Span = Assert.IsType<Span>(tb1.Inlines.FirstInline);

            var tb2 = new TextBlock();
            _renderer.Render("## H2", tb2.Inlines);
            var h2Span = Assert.IsType<Span>(tb2.Inlines.FirstInline);

            Assert.True(h1Span.FontSize > h2Span.FontSize);
        });
    }

    // ─── Link ─────────────────────────────────────────────────────────

    [Fact]
    public void Link_RendersAsHyperlink()
    {
        RenderAndAssert("[click](https://example.com)", inlines =>
        {
            var hyperlink = Assert.IsType<Hyperlink>(inlines[0]);
            Assert.Equal("https://example.com/", hyperlink.NavigateUri.ToString());
            Assert.Equal("click", ((Run)hyperlink.Inlines.FirstInline!).Text);
        });
    }

    // ─── Image ────────────────────────────────────────────────────────

    [Fact]
    public void Image_RendersAsTextFallback()
    {
        RenderAndAssert("![alt text](https://example.com/img.png)", inlines =>
        {
            var run = Assert.IsType<Run>(inlines[0]);
            Assert.Contains("alt text", run.Text);
        });
    }

    // ─── Unordered list ───────────────────────────────────────────────

    [Fact]
    public void UnorderedList_RendersBullets()
    {
        RenderAndAssert("- one\n- two\n- three", inlines =>
        {
            var text = string.Join("", inlines.OfType<Run>().Select(r => r.Text));
            Assert.Contains("•", text);
            Assert.Contains("one", text);
            Assert.Contains("two", text);
            Assert.Contains("three", text);
        });
    }

    // ─── Ordered list ─────────────────────────────────────────────────

    [Fact]
    public void OrderedList_RendersNumbers()
    {
        RenderAndAssert("1. first\n2. second", inlines =>
        {
            var text = string.Join("", inlines.OfType<Run>().Select(r => r.Text));
            Assert.Contains("1.", text);
            Assert.Contains("2.", text);
            Assert.Contains("first", text);
            Assert.Contains("second", text);
        });
    }

    // ─── Blockquote ───────────────────────────────────────────────────

    [Fact]
    public void Blockquote_RendersWithQuoteMark()
    {
        RenderAndAssert("> quoted text", inlines =>
        {
            var span = Assert.IsType<Span>(inlines[0]);
            Assert.Equal(FontStyles.Italic, span.FontStyle);
            var allRuns = span.Inlines.OfType<Run>().ToList();
            Assert.Contains(allRuns, r => r.Text.Contains("│"));
            Assert.Contains(allRuns, r => r.Text.Contains("quoted text"));
        });
    }

    // ─── Thematic break ───────────────────────────────────────────────

    [Fact]
    public void ThematicBreak_RendersHorizontalRule()
    {
        RenderAndAssert("above\n\n---\n\nbelow", inlines =>
        {
            var runs = inlines.OfType<Run>().ToList();
            Assert.Contains(runs, r => r.Text.Contains("────"));
        });
    }

    // ─── Mixed content ────────────────────────────────────────────────

    [Fact]
    public void Mixed_BoldAndPlainText()
    {
        RenderAndAssert("Hello **world** today", inlines =>
        {
            Assert.True(inlines.Count >= 3);
            Assert.IsType<Run>(inlines[0]);
            Assert.Equal("Hello ", ((Run)inlines[0]).Text);
            var boldSpan = Assert.IsType<Span>(inlines[1]);
            Assert.Equal(FontWeights.Bold, boldSpan.FontWeight);
        });
    }

    // ─── Empty / whitespace ───────────────────────────────────────────

    [Fact]
    public void EmptyString_ProducesNoInlines()
    {
        RenderAndAssert("", inlines => Assert.Empty(inlines));
    }

    [Fact]
    public void WhitespaceOnly_ProducesNoInlines()
    {
        RenderAndAssert("   \n\n  ", inlines => Assert.Empty(inlines));
    }

    // ─── Incomplete markdown (streaming) ──────────────────────────────

    [Fact]
    public void IncompleteMarkdown_DoesNotCrash()
    {
        RenderAndAssert("Hello **world", inlines => Assert.NotEmpty(inlines));
    }

    [Fact]
    public void IncompleteCodeFence_DoesNotCrash()
    {
        RenderAndAssert("```python\nprint('hi')", inlines => Assert.NotEmpty(inlines));
    }

    // ─── HTML entities ────────────────────────────────────────────────

    [Fact]
    public void HtmlEntity_DecodesCorrectly()
    {
        RenderAndAssert("&amp; &lt; &gt;", inlines =>
        {
            var text = string.Join("", inlines.OfType<Run>().Select(r => r.Text));
            Assert.Contains("&", text);
            Assert.Contains("<", text);
            Assert.Contains(">", text);
        });
    }

    // ─── Multiple paragraphs ──────────────────────────────────────────

    [Fact]
    public void MultipleParagraphs_SeparatedByLineBreaks()
    {
        RenderAndAssert("First paragraph\n\nSecond paragraph", inlines =>
        {
            var breaks = inlines.OfType<LineBreak>().ToList();
            Assert.True(breaks.Count >= 1, "Paragraphs should be separated by LineBreak");
        });
    }

    // ─── Nested list ──────────────────────────────────────────────────

    [Fact]
    public void NestedList_RendersWithIndent()
    {
        RenderAndAssert("- item\n  - nested", inlines =>
        {
            var text = string.Join("", inlines.OfType<Run>().Select(r => r.Text));
            Assert.Contains("item", text);
            Assert.Contains("nested", text);
        });
    }
}

