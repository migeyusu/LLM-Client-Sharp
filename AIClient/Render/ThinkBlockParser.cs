
using LLMClient.Render;
using Markdig.Parsers;
using Markdig.Syntax;

public class ThinkBlockParser : BlockParser
{
    private const string OpenTag = "<think>";
    private const string CloseTag = "</think>";
    
    public ThinkBlockParser()
    {
        OpeningCharacters = new[] { '<' };
    }

    public override BlockState TryOpen(BlockProcessor processor)
    {
        if (processor.IsCodeIndent)
            return BlockState.None;

        var line = processor.Line;
        
        // 检查是否以 <think> 开头
        if (!line.Match(OpenTag))
            return BlockState.None;
        
        // 创建ThinkBlock
        var startPosition = processor.Start;
        var thinkBlock = new ThinkBlock(this)
        {
            Column = processor.Column,
            Span = new SourceSpan(startPosition, line.End)
        };
        processor.NewBlocks.Push(thinkBlock);
        
        // 跳过 <think> 标签
        line.Start += OpenTag.Length;
        
        // 检查同一行是否有内容或结束标签
        if (line.IsEmpty)
        {
            // 如果<think>后面直接是换行，跳过这个空行
            return BlockState.ContinueDiscard;
        }
        
        // 继续处理当前行剩余内容
        return CheckForEnd(processor, thinkBlock);
    }

    public override BlockState TryContinue(BlockProcessor processor, Block block)
    {
        var thinkBlock = (ThinkBlock)block;
        return CheckForEnd(processor, thinkBlock);
    }

    private BlockState CheckForEnd(BlockProcessor processor, ThinkBlock thinkBlock)
    {
        var line = processor.Line;
        
        // 查找 </think>
        var endIndex = line.IndexOf(CloseTag);
        
        if (endIndex >= 0)
        {
            // 找到结束标签
            if (endIndex > line.Start)
            {
                // 只保留</think>之前的内容
                processor.Line.End = line.Start + endIndex - 1;
                return BlockState.Break;
            }
            else
            {
                // 如果</think>在行首，跳过整行
                return BlockState.BreakDiscard;
            }
        }
        
        // 没找到结束标签，继续处理下一行
        return BlockState.Continue;
    }
}