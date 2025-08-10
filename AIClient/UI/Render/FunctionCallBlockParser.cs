namespace LLMClient.UI.Render;

[Obsolete]
public class FunctionCallBlockParser : SingleTagBlockParser<FunctionCallBlock>
{
    public const string FunctionCallTag = "<function_call>";

    public const string FunctionCallEndTag = "</function_call>";

    public FunctionCallBlockParser() : base(FunctionCallTag, FunctionCallEndTag)
    {
        
    }
}