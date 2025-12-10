using Markdig.Extensions.Mathematics;
using Markdig.Helpers;
using Markdig.Parsers;
using Markdig.Syntax;

namespace LLMClient.Component.Render;

public class LatexMathInlineParser : InlineParser
{
    public LatexMathInlineParser()
    {
        OpeningCharacters = ['\\'];
        DefaultClass = "math";
    }

    /// <summary>
    /// Gets or sets the default class to use when creating a math inline block.
    /// </summary>
    public string DefaultClass { get; set; }

    public override bool Match(InlineProcessor processor, ref StringSlice slice)
    {
        // 向前看一个字符，检查是否是 '\(' 开头
        if (slice.Length < 2 || slice.CurrentChar != '\\' || slice.PeekChar(1) != '(')
        {
            return false;
        }

        char previousChar = slice.PeekCharExtra(-1);
        if (!CanOpen(previousChar))
        {
            return false;
        }

        var startPosition = slice.Start;

        // 寻找结束标记 '\)'
        slice.Start += 2; // 跳过 '\('
        int contentStart = slice.Start;
        while (slice.Start < slice.End)
        {
            // 找到潜在的结束标记
            if (slice.CurrentChar == '\\' && slice.PeekChar(1) == ')')
            {
                var contentEnd = slice.Start - 1;
                char nextChar = slice.PeekChar(2);
                if (!CanClose(nextChar))
                {
                    slice.Start++;
                    continue;
                }

                processor.Inline = new MathInline()
                {
                    Content = new StringSlice(slice.Text, contentStart, contentEnd),
                    Span = new SourceSpan(processor.GetSourcePosition(startPosition, out int line, out int column),
                        processor.GetSourcePosition(slice.Start + 1)),
                    Line = line,
                    Column = column,
                };

                slice.Start += 2; // 消费掉 '\)'
                return true;
            }

            // 处理转义的 '\'
            if (slice.CurrentChar == '\\' && slice.PeekChar(1) == '\\')
            {
                slice.Start++;
            }

            slice.Start++;
        }

        // 未找到合法的结束标记，匹配失败，回滚
        slice.Start = startPosition;
        return false;
    }

    private static bool CanOpen(char previousChar) =>
        char.IsWhiteSpace(previousChar) || char.IsPunctuation(previousChar) || previousChar == '\0';

    private static bool CanClose(char nextChar) =>
        char.IsWhiteSpace(nextChar) || char.IsPunctuation(nextChar) || nextChar == '\0';
}