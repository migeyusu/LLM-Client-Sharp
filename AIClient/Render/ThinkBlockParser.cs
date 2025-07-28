namespace LLMClient.Render;

public class ThinkBlockParser : SingleTagBlockParser<ThinkBlock>
{
    public const string OpenTag = "<think>";

    public const string CloseTag = "</think>";

    public ThinkBlockParser() : base(OpenTag, CloseTag)
    {
        
    }
}