using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows.Documents;
using Markdig;
using Markdig.Helpers;
using Markdig.Parsers;
using Markdig.Parsers.Inlines;
using Markdig.Renderers;
using Markdig.Syntax;
using Block = Markdig.Syntax.Block;

namespace LLMClient.Component.Render;

public static class RendererExtensions
{
    static RendererExtensions()
    {
    }

    public static bool TryParseMarkdown(string raw, [NotNullWhen(true)] out MarkdownDocument? document)
    {
        try
        {
            document = Markdown.Parse(raw, CustomMarkdownRenderer.DefaultPipeline);
            return true;
        }
        catch (Exception ex)
        {
            // Log the error or handle it as needed
            Trace.TraceWarning($"Error parsing markdown: {ex.Message}");
            document = null;
            return false;
        }
    }

    /// <summary>
    /// 对 Markdig.Parsers.InlineProcessor.Rent 的高性能劫持调用
    /// </summary>
    /// <param name="dummy">占位符，JIT 需要它来确定目标类类型，调用时传 null</param>
    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "Rent")]
    public static extern InlineProcessor RentInlineProcessor(
        InlineProcessor? dummy,
        MarkdownDocument document,
        InlineParserList parsers,
        bool preciseSourceLocation,
        MarkdownParserContext? context,
        bool trackTrivia);

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "Release")]
    public static extern void ReleaseInlineProcessor(InlineProcessor? dummy, InlineProcessor processor);

    public static void StreamParse(BlockingCollection<string> source, ProcessBlockDelegate blockParserCallback)
    {
        var defaultBuilder = CustomMarkdownRenderer.DefaultBuilder;
        var blockParsers = defaultBuilder.BlockParsers;
        var inlineParsers = defaultBuilder.InlineParsers;
        var document = new MarkdownDocument
        {
            LineStartIndexes = new List<int>(512)
        };

        var inlineProcessor =
            RentInlineProcessor(null, document, new InlineParserList(inlineParsers), true, null, false);
        try
        {
            foreach (var blockParser in blockParsers)
            {
                blockParser.Closed += (closedParser, block) =>
                {
                    var rentInlineProcessor = RentInlineProcessor(null, document, new InlineParserList(inlineParsers),
                        true, null, false);
                    try
                    {
                        switch (block)
                        {
                            case ContainerBlock containerBlock:
                                ProcessInlines(inlineProcessor, containerBlock);
                                break;
                            case LeafBlock leafBlock:
                                ProcessInlines(inlineProcessor, leafBlock);
                                break;
                        }

                        blockParserCallback(closedParser, block);
                    }
                    catch (Exception e)
                    {
                        Trace.TraceWarning("Error processing block: " + e.Message);
                    }
                    finally
                    {
                        ReleaseInlineProcessor(null, rentInlineProcessor);
                    }
                };
            }
        }
        finally
        {
            ReleaseInlineProcessor(null, inlineProcessor);
        }

        var blockProcessor =
            new BlockProcessor(document, new BlockParserList(blockParsers), null);
        ProcessBlocksStreaming(blockProcessor, source);
        document.LineCount = blockProcessor.LineIndex;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ProcessBlocksStreaming(BlockProcessor blockProcessor, BlockingCollection<string> blocks)
    {
        var lineReader = new LineReaderBuffer();
        foreach (var block in blocks.GetConsumingEnumerable())
        {
            lineReader.AppendText(block);
            foreach (var newLine in lineReader.ExtractLines())
            {
                blockProcessor.ProcessLine(newLine);
            }
        }

        var lastSlice = lineReader.End();
        if (lastSlice != null)
        {
            blockProcessor.ProcessLine(lastSlice.Value);
        }

        typeof(BlockProcessor).GetMethod("CloseAll",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?.Invoke(blockProcessor, [true]);
    }

    public readonly struct LineReaderBuffer
    {
        private readonly StringBuilder _buffer = new(4096);

        /// <summary>
        /// Initializes a new instance of the <see cref="Markdig.Helpers.LineReader"/> class.
        /// </summary>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ArgumentOutOfRangeException">bufferSize cannot be &lt;= 0</exception>
        public LineReaderBuffer()
        {
        }

        private const char CR = '\r';
        private const char LF = '\n';

        public void AppendText(string text)
        {
            _buffer.Append(text);
        }

        public IEnumerable<StringSlice> ExtractLines()
        {
            while (true)
            {
                int eolIndex = -1;
                int offset = 0;

                // 总是重新获取chunks进行扫描，因为在循环中会通过Remove修改buffer结构，
                // 导致之前的迭代器或索引失效。
                foreach (var chunk in _buffer.GetChunks())
                {
                    var index = chunk.Span.IndexOfAny(CR, LF);
                    if (index >= 0)
                    {
                        eolIndex = offset + index;
                        break;
                    }

                    offset += chunk.Span.Length;
                }

                if (eolIndex < 0)
                {
                    yield break;
                }

                var newSourcePosition = eolIndex + 1;
                var newLine = NewLine.None;

                // 检查换行符类型
                if (_buffer[eolIndex] == CR)
                {
                    if (newSourcePosition < _buffer.Length && _buffer[newSourcePosition] == LF)
                    {
                        newLine = NewLine.CarriageReturnLineFeed;
                        newSourcePosition++;
                    }
                    else
                    {
                        newLine = NewLine.CarriageReturn;
                    }
                }
                else
                {
                    newLine = NewLine.LineFeed;
                }

                // 提取行并从buffer中移除
                var line = _buffer.ToString(0, eolIndex);
                _buffer.Remove(0, newSourcePosition);

                // StringSlice 的 end 是 inclusive 的，所以是 length - 1
                yield return new StringSlice(line, 0, line.Length - 1, newLine);
            }
        }

        public StringSlice? End()
        {
            if (_buffer.Length > 0)
            {
                var text = _buffer.ToString();
                _buffer.Clear();
                return new StringSlice(text, 0, text.Length - 1, NewLine.None);
            }

            return null;
        }
    }

    private struct ContainerItem(ContainerBlock container)
    {
        public readonly ContainerBlock Container = container;

        public int Index = 0;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ProcessInlines(InlineProcessor inlineProcessor, LeafBlock block)
    {
        var currentBlock = (Block)block;

        while (true)
        {
            if (currentBlock is LeafBlock leafBlock)
            {
                var currentLeafBlock = leafBlock;

                if (currentLeafBlock.ProcessInlines)
                {
                    // 处理内联元素  
                    inlineProcessor.ProcessInlineLeaf(currentLeafBlock);

                    // 检查是否有块替换  
                    if (inlineProcessor.BlockNew != null)
                    {
                        currentBlock = inlineProcessor.BlockNew;
                        inlineProcessor.BlockNew = null; // 重置状态  
                        continue; // 重新处理新块  
                    }
                }

                break;
            }

            if (currentBlock is ContainerBlock containerBlock)
            {
                ProcessInlines(inlineProcessor, containerBlock);
            }
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ProcessInlines(InlineProcessor inlineProcessor, ContainerBlock containerBlock)
    {
        // "stackless" processor
        int blockCount = 1;
        var blocks = new ContainerItem[4];

        blocks[0] = new ContainerItem(containerBlock);

        while (blockCount != 0)
        {
            process_new_block:
            ref ContainerItem item = ref blocks[blockCount - 1];
            var container = item.Container;

            for (; item.Index < container.Count; item.Index++)
            {
                var block = container[item.Index];
                if (block is LeafBlock leafBlock)
                {
                    if (leafBlock.ProcessInlines)
                    {
                        if (leafBlock.Lines.Count > 0)
                        {
                            inlineProcessor.ProcessInlineLeaf(leafBlock);
                        }

                        if (leafBlock.RemoveAfterProcessInlines)
                        {
                            container.RemoveAt(item.Index);
                            item.Index--;
                        }
                        else if (inlineProcessor.BlockNew != null)
                        {
                            container[item.Index] = inlineProcessor.BlockNew;
                        }
                    }
                }
                else if (block is ContainerBlock containerBlock2)
                {
                    // If we need to remove it
                    if (block.RemoveAfterProcessInlines)
                    {
                        container.RemoveAt(item.Index);
                    }
                    else
                    {
                        // Else we have processed it
                        item.Index++;
                    }

                    if (blockCount == blocks.Length)
                    {
                        Array.Resize(ref blocks, blockCount * 2);
                    }

                    blocks[blockCount++] = new ContainerItem(containerBlock2);
                    goto process_new_block;
                }
            }

            blocks[--blockCount] = default;
        }
    }

    public static void WriteRawLines(this WpfRenderer renderer, StringLineGroup lines1)
    {
        StringLine[] lines2 = lines1.Lines;
        for (int index = 0; index < lines1.Count; ++index)
        {
            if (index != 0)
                renderer.WriteInline(new LineBreak());
            renderer.WriteText(ref lines2[index].Slice);
        }
    }

    public static FlowDocument RenderOnFlowDocument(this string raw, FlowDocument? result = null)
    {
        result ??= new FlowDocument();
        CustomMarkdownRenderer.DefaultInstance.RenderRaw(raw, result);
        return result;
    }

    internal static string SubstringAtIndexes(this string str, int startIndex, int endIndex)
    {
        return str.Substring(startIndex, endIndex - startIndex);
    }

    public static MarkdownPipelineBuilder UseThinkBlock(
        this MarkdownPipelineBuilder pipeline)
    {
        pipeline.Extensions.ReplaceOrAdd<ThinkBlockExtension>(new ThinkBlockExtension());
        return pipeline;
    }

    public static MarkdownPipelineBuilder UseFunctionCallBlock(
        this MarkdownPipelineBuilder pipeline)
    {
        pipeline.Extensions.ReplaceOrAdd<ToolCallBlockExtension>(new ToolCallBlockExtension());
        return pipeline;
    }

    public static MarkdownPipelineBuilder UseFunctionResultBlock(
        this MarkdownPipelineBuilder pipeline)
    {
        pipeline.Extensions.ReplaceOrAdd<ToolCallResultBlockExtension>(new ToolCallResultBlockExtension());
        return pipeline;
    }

    public static MarkdownPipelineBuilder UseRequestBlock(
        this MarkdownPipelineBuilder pipeline)
    {
        pipeline.Extensions.ReplaceOrAdd<RequestBlockExtension>(new RequestBlockExtension());
        return pipeline;
    }

    public static MarkdownPipelineBuilder UseCustomMathematics(
        this MarkdownPipelineBuilder pipeline)
    {
        pipeline.Extensions.ReplaceOrAdd<CustomMathBlockExtension>(new CustomMathBlockExtension());
        return pipeline;
    }
}