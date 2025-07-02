namespace LLMClient.Render;

public class FunctionCallBlockParser : SingleTagBlockParser<FunctionCallBlock>
{
    public const string FunctionCallTag = "<function_call>";

    public const string FunctionCallEndTag = "</function_call>";

    public FunctionCallBlockParser() : base(FunctionCallTag, FunctionCallEndTag)
    {
        
    }
}