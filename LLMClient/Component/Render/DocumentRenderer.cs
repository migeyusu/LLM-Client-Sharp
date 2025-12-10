using System.Runtime.CompilerServices;
using System.Windows.Documents;
using System.Windows.Markup;
using Markdig.Helpers;
using Markdig.Renderers;
using Markdig.Renderers.Wpf;
using Markdig.Renderers.Wpf.Extensions;
using Markdig.Renderers.Wpf.Inlines;
using Markdig.Syntax;

namespace LLMClient.Component.Render;

public class DocumentRenderer : RendererBase
{
    private readonly Stack<IAddChild> stack = new Stack<IAddChild>();

    private char[] buffer;

    public DocumentRenderer() => this.buffer = new char[1024 /*0x0400*/];

    public IAddChild Root { get; }
    
    public DocumentRenderer(IAddChild root)
    {
        this.buffer = new char[1024 /*0x0400*/];
        this.Root = root;
        this.stack.Push(root);
    }

    public override object Render(MarkdownObject markdownObject)
    {
        throw new NotSupportedException();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteLeafInline(LeafBlock leafBlock)
    {
        if (leafBlock == null)
            throw new ArgumentNullException(nameof(leafBlock));
        for (Markdig.Syntax.Inlines.Inline inline = (Markdig.Syntax.Inlines.Inline)leafBlock.Inline;
             inline != null;
             inline = inline.NextSibling)
            this.Write((MarkdownObject)inline);
    }

    public void WriteLeafRawLines(LeafBlock leafBlock)
    {
        if (leafBlock == null)
            throw new ArgumentNullException(nameof(leafBlock));
        if (leafBlock.Lines.Lines == null)
            return;
        StringLineGroup lines1 = leafBlock.Lines;
        StringLine[] lines2 = lines1.Lines;
        for (int index = 0; index < lines1.Count; ++index)
        {
            if (index != 0)
                this.WriteInline((System.Windows.Documents.Inline)new LineBreak());
            this.WriteText(ref lines2[index].Slice);
        }
    }

    public void Push(IAddChild o) => this.stack.Push(o);

    public void Pop()
    {
        IAddChild addChild = this.stack.Pop();
        this.stack.Peek().AddChild((object)addChild);
    }

    public void WriteBlock(System.Windows.Documents.Block block)
    {
        this.stack.Peek().AddChild((object)block);
    }

    public void WriteInline(System.Windows.Documents.Inline inline)
    {
        DocumentRenderer.AddInline(this.stack.Peek(), inline);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteText(ref StringSlice slice)
    {
        if (slice.Start > slice.End)
            return;
        this.WriteText(slice.Text, slice.Start, slice.Length);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteText(string? text) => this.WriteInline((System.Windows.Documents.Inline)new Run(text));

    public void WriteText(string? text, int offset, int length)
    {
        if (text == null)
            return;
        if (offset == 0 && text.Length == length)
            this.WriteText(text);
        else if (length > this.buffer.Length)
        {
            this.buffer = text.ToCharArray();
            this.WriteText(new string(this.buffer, offset, length));
        }
        else
        {
            text.CopyTo(offset, this.buffer, 0, length);
            this.WriteText(new string(this.buffer, 0, length));
        }
    }

    protected virtual void LoadRenderers()
    {
        this.ObjectRenderers.Add((IMarkdownObjectRenderer)new CodeBlockRenderer());
        this.ObjectRenderers.Add((IMarkdownObjectRenderer)new ListRenderer());
        this.ObjectRenderers.Add((IMarkdownObjectRenderer)new HeadingRenderer());
        this.ObjectRenderers.Add((IMarkdownObjectRenderer)new ParagraphRenderer());
        this.ObjectRenderers.Add((IMarkdownObjectRenderer)new QuoteBlockRenderer());
        this.ObjectRenderers.Add((IMarkdownObjectRenderer)new ThematicBreakRenderer());
        this.ObjectRenderers.Add((IMarkdownObjectRenderer)new AutolinkInlineRenderer());
        this.ObjectRenderers.Add((IMarkdownObjectRenderer)new CodeInlineRenderer());
        this.ObjectRenderers.Add((IMarkdownObjectRenderer)new DelimiterInlineRenderer());
        this.ObjectRenderers.Add((IMarkdownObjectRenderer)new EmphasisInlineRenderer());
        this.ObjectRenderers.Add((IMarkdownObjectRenderer)new HtmlEntityInlineRenderer());
        this.ObjectRenderers.Add((IMarkdownObjectRenderer)new LineBreakInlineRenderer());
        this.ObjectRenderers.Add((IMarkdownObjectRenderer)new LinkInlineRenderer());
        this.ObjectRenderers.Add((IMarkdownObjectRenderer)new LiteralInlineRenderer());
        this.ObjectRenderers.Add((IMarkdownObjectRenderer)new TableRenderer());
        this.ObjectRenderers.Add((IMarkdownObjectRenderer)new TaskListRenderer());
    }

    private static void AddInline(IAddChild parent, System.Windows.Documents.Inline inline)
    {
        parent.AddChild((object)inline);
    }
}