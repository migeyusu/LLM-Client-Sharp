using System.Windows;


namespace LLMClient;

public class MarkdownStyles
{
    public static ComponentResourceKey CodeCopyButtonStyleKey { get; } =
        new ComponentResourceKey(typeof(MarkdownStyles), (object)nameof(CodeCopyButtonStyleKey));

    public static ComponentResourceKey CodeBlockHeaderStyleKey { get; } =
        new ComponentResourceKey(typeof(MarkdownStyles), (object)nameof(CodeBlockHeaderStyleKey));
}