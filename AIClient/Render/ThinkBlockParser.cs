namespace LLMClient.Render;

public class ThinkBlockParser : SingleTagBlockParser<ThinkBlock>
{
    private const string OpenTag = "<think>";

    private const string CloseTag = "</think>";

    public ThinkBlockParser() : base(OpenTag, CloseTag)
    {
        
    }
}