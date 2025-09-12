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
using Markdig.Syntax;
using Block = Markdig.Syntax.Block;

namespace LLMClient.UI.Render;

public static class RendererExtensions
{
    static RendererExtensions()
    {
    }

    public static bool TryParseMarkdown(string raw, [NotNullWhen(true)] out MarkdownDocument? document)
    {
        try
        {
            document = Markdown.Parse(raw, CustomRenderer.DefaultPipeline);
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

    public static void StreamParse(BlockingCollection<string> source, ProcessBlockDelegate blockParserCallback)
    {
        var blockParsers = new BlockParser[]
        {
            new ThematicBreakParser(),
            new HeadingBlockParser(),
            new QuoteBlockParser(),
            new ListBlockParser(),

            new HtmlBlockParser(),
            new FencedCodeBlockParser(),
            new IndentedCodeBlockParser(),
            new ParagraphBlockParser(),
        };
        var inlineParsers = new InlineParser[]
        {
            new HtmlEntityParser(),
            new LinkInlineParser(),
            new EscapeInlineParser(),
            new EmphasisInlineParser(),
            new CodeInlineParser(),
            new AutolinkInlineParser(),
            new LineBreakInlineParser(),
        };

        var document = new MarkdownDocument
        {
            LineStartIndexes = new List<int>(512)
        };

        var processor = new InlineProcessor(document, new InlineParserList(inlineParsers), true, null);
        foreach (var blockParser in blockParsers)
        {
            blockParser.Closed += (processor1, block) =>
            {
                try
                {
                    if (block is ContainerBlock containerBlock)
                    {
                        ProcessInlines(processor, containerBlock);
                    }
                    else if (block is LeafBlock leafBlock)
                    {
                        ProcessInlines(processor, leafBlock);
                    }

                    blockParserCallback(processor1, block);
                }
                catch (Exception e)
                {
                    Trace.TraceWarning("Error processing block: " + e.Message);
                }
            };
        }

        var blockProcessor =
            new BlockProcessor(document, new BlockParserList(blockParsers), null);
        ProcessBlocksStreaming(blockProcessor, source);
        document.LineCount = blockProcessor.LineIndex;


        /*var inlineProcessor = InlineProcessor.Rent(document, pipeline.InlineParsers, pipeline.PreciseSourceLocation,
            context, pipeline.TrackTrivia);
        inlineProcessor.DebugLog = pipeline.DebugLog;
        try
        {
            ProcessInlines(inlineProcessor, document);
        }
        finally
        {
            InlineProcessor.Release(inlineProcessor);
        }

        // Allow to call a hook after processing a document
        pipeline.DocumentProcessed?.Invoke(document);*/
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ProcessBlocksStreaming(BlockProcessor blockProcessor, BlockingCollection<string> blocks)
    {
        var lineReader = new LineReaderBuffer();
        foreach (var block in blocks.GetConsumingEnumerable())
        {
            var newLine = lineReader.AppendTextAndExtractLine(block);
            if (newLine != null)
            {
                blockProcessor.ProcessLine(newLine.Value);
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
        private readonly StringBuilder _buffer = new StringBuilder(4096);

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

        public StringSlice? AppendTextAndExtractLine(string text)
        {
            _buffer.Append(text);
            var chunkEnumerator = _buffer.GetChunks();
            if (chunkEnumerator.MoveNext())
            {
                var span = chunkEnumerator.Current.Span;
                //如果text的换行符是分开的，可以接收这种错误
                var newLine = NewLine.None; // 默认换行符类型
                int end = 0;
                if ((end = span.IndexOfAny(CR, LF)) >= 0)
                {
                    var newSourcePosition = end + 1;
                    if (_buffer[end] == CR)
                    {
                        // 检查回车后是否跟着换行符(\n)，识别为CRLF序列
                        if ((uint)(newSourcePosition) < (uint)_buffer.Length && _buffer[newSourcePosition] == LF)
                        {
                            newLine = NewLine.CarriageReturnLineFeed; // \r\n
                            newSourcePosition++; // 跳过\n
                        }
                        else
                        {
                            newLine = NewLine.CarriageReturn; // 仅\r
                        }
                    }
                    else
                    {
                        newLine = NewLine.LineFeed; // 仅\n
                    }

                    var line = _buffer.ToString(0, end);
                    _buffer.Remove(0, newSourcePosition);
                    return new StringSlice(line, 0, end - 1, newLine);
                }
            }

            return null;
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

    public static FlowDocument RenderOnFlowDocument(this string raw, FlowDocument? result = null)
    {
        result ??= new FlowDocument();
        CustomRenderer.Instance.RenderRaw(raw, result);
        return result;
    }

    //todo: markdig渲染 改进： 1. 支持动态增加obj，每次循环后在原有FlowDocument基础上增加 2. 支持动态增加文本

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

    [Obsolete]
    public static MarkdownPipelineBuilder UseFunctionCallBlock(
        this MarkdownPipelineBuilder pipeline)
    {
        pipeline.Extensions.ReplaceOrAdd<FunctionCallBlockExtension>(new FunctionCallBlockExtension());
        return pipeline;
    }

    [Obsolete]
    public static MarkdownPipelineBuilder UseFunctionResultBlock(
        this MarkdownPipelineBuilder pipeline)
    {
        pipeline.Extensions.ReplaceOrAdd<FunctionResultBlockExtension>(new FunctionResultBlockExtension());
        return pipeline;
    }
}