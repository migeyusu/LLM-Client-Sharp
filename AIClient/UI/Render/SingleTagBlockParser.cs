using System.Diagnostics;
using Markdig.Parsers;
using Markdig.Syntax;

namespace LLMClient.UI.Render;

public abstract class SingleTagBlockParser<T> : BlockParser where T : CustomLeafBlock
{
    public bool IgnoreCase { get; set; } = true;

    private readonly string _openTag;
    private readonly string _closeTag;

    public SingleTagBlockParser(string openTag, string closeTag)
    {
        _openTag = openTag;
        _closeTag = closeTag;
        // 当一行以 '<' 开头时，Markdig 会尝试调用这个解析器
        OpeningCharacters = [openTag.First()];
    }

    public override BlockState TryOpen(BlockProcessor processor)
    {
        // 如果是在一个代码块缩进中，则不处理
        if (processor.IsCodeIndent)
        {
            return BlockState.None;
        }

        if (processor.IsBlankLine)
        {
            return BlockState.None;
        }

        // 检查当前行是否以 tag 开头(忽略大小写)
        var line = processor.Line;
        int index;
        if ((index = line.IndexOf(_openTag, 0, IgnoreCase)) < 0) // true for case-insensitive
        {
            return BlockState.None;
        }

        var customBlock = Activator.CreateInstance(typeof(T), this) as T;
        // 设置块的起始位置和列号
        Debug.Assert(customBlock != null, nameof(customBlock) + " != null");
        customBlock.Column = processor.Column;
        customBlock.Line = processor.LineIndex;
        customBlock.Span = new SourceSpan(line.Start, line.End);
        line.Start = index + _openTag.Length; // 更新行的起始位置，跳过标签
        if (processor.TrackTrivia)
        {
            customBlock.LinesBefore = processor.LinesBefore;
            processor.LinesBefore = null;
            customBlock.NewLine = processor.Line.NewLine;
        }

        // 将新块推送到处理器中
        processor.NewBlocks.Push(customBlock);
        // 检查是否在同一行就闭合了
        if (line.IndexOf(_closeTag, 0, IgnoreCase) >= 0)
        {
            // 如果在同一行闭合，则直接关闭块
            return BlockState.Break;
        }

        // 否则，继续处理下一行
        return BlockState.Continue;
    }

    public override BlockState TryContinue(BlockProcessor processor, Block block)
    {
        // 检查当前行是否包含结束标签
        if (processor.IsBlankLine)
        {
            // 如果是空行，继续
            return BlockState.Continue;
        }

        var line = processor.Line;
        if (line.IndexOf(_closeTag, 0, IgnoreCase) >= 0)
        {
            // 找到结束标签，将当前行加入并关闭块
            return BlockState.Break;
        }

        block.NewLine = processor.Line.NewLine;
        block.UpdateSpanEnd(processor.Line.End);
        return BlockState.Continue;
    }

    public override bool Close(BlockProcessor processor, Block block)
    {
        if (block is T customBlock)
        {
            // 在关闭块时，从最后一行移除标签
            ref var lastLine = ref customBlock.Lines.Lines[customBlock.Lines.Count - 1];
            var endIndex = lastLine.Slice.IndexOf(_closeTag, 0, IgnoreCase);
            if (endIndex != -1)
            {
                // 更新最后一行的结束位置，以去除闭合标签
                lastLine.Slice.End = endIndex - 1;
            }

            ref var firstLine = ref customBlock.Lines.Lines[0];
            var startIndex = firstLine.Slice.IndexOf(_openTag, 0, IgnoreCase);
            if (startIndex != -1)
            {
                // 更新第一行的起始位置，以去除开始标签
                firstLine.Slice.Start = startIndex + _openTag.Length;
            }

            // 更新块的结束位置
            customBlock.UpdateSpanEnd(endIndex + _closeTag.Length);
            PostProcess(customBlock);
        }

        return true;
    }

    /// <summary>
    /// called after the block is closed
    /// </summary>
    /// <param name="block"></param>
    protected virtual void PostProcess(T block)
    {
    }
}