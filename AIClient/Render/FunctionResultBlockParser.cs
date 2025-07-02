namespace LLMClient.Render;

public class FunctionResultBlockParser : SingleTagBlockParser<FunctionResultBlock>
{
    public const string FunctionResultTag = "<function_result>";

    public const string FunctionResultEndTag = "</function_result>";

    public FunctionResultBlockParser() : base(FunctionResultTag, FunctionResultEndTag)
    {
        
    }
}