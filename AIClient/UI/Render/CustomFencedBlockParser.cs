using System.Diagnostics;
using Markdig.Helpers;
using Markdig.Parsers;
using Markdig.Syntax;

namespace LLMClient.UI.Render;

/// <summary>
/// 使用独立行的开始和结束标签来定义一个块
/// </summary>
/// <typeparam name="T"></typeparam>
public abstract class CustomFencedBlockParser<T> : FencedBlockParserBase where T : FencedCodeBlock
{
    public bool IgnoreCase { get; set; } = true;

    private readonly string _openTag;

    private readonly string _closeTag;

    public CustomFencedBlockParser(string openTag, string closeTag)
    {
        _openTag = openTag;
        _closeTag = closeTag;
        // 当一行以 '<' 开头时，Markdig 会尝试调用这个解析器
        OpeningCharacters = [openTag.First()];
    }

    private static bool NoInfoParser(BlockProcessor state, ref StringSlice line, IFencedBlock fenced,
        char openingCharacter)
    {
        for (int i = line.Start; i <= line.End; i++)
        {
            if (!line.Text[i].IsSpaceOrTab())
            {
                return false;
            }
        }

        return true;
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

        // 检查当前行是否为 tag 开头(忽略大小写)
        var line = processor.Line;
        int index;
        if ((index = line.IndexOf(_openTag, 0, IgnoreCase)) != 0) // true for case-insensitive
        {
            return BlockState.None;
        }

        if (line.ToString().Trim() != _openTag)
        {
            return BlockState.None;
        }

        var customBlock = CreateBlock(processor);
        customBlock.Column = processor.Column;
        customBlock.FencedChar = line.CurrentChar;
        customBlock.OpeningFencedCharCount = _openTag.Length;
        customBlock.Line = processor.LineIndex;
        customBlock.Span = new SourceSpan(line.Start, line.End);
        if (processor.TrackTrivia)
        {
            customBlock.LinesBefore = processor.LinesBefore;
            processor.LinesBefore = null;
            customBlock.NewLine = processor.Line.NewLine;
        }

        processor.NewBlocks.Push(customBlock);
        return BlockState.ContinueDiscard;
    }

    public override BlockState TryContinue(BlockProcessor processor, Block block)
    {
        var fence = (T)block;
        var line = processor.Line;
        // 检查当前行是否包含结束标签
        if (processor.IsBlankLine || processor.IsCodeIndent || line.IndexOf(_closeTag, 0, IgnoreCase) != 0)
        {
            processor.GoToColumn(processor.ColumnBeforeIndent);
            return BlockState.Continue;
        }

        fence.ClosingFencedCharCount = _closeTag.Length;
        var lineEnd = line.End;
        block.UpdateSpanEnd(lineEnd);
        if (processor.TrackTrivia)
        {
            block.NewLine = line.NewLine;
            var processorStart = processor.Start;
            fence.TriviaAfterFencedChar = processor.UseTrivia(processorStart - 1);
            fence.TriviaAfter = new StringSlice(line.Text, processorStart + _closeTag.Length, lineEnd);
        }

        return BlockState.BreakDiscard;
    }

    protected abstract T CreateBlock(BlockProcessor processor);
}