namespace LLMClient.Component.Render;

public class RequestBlockParser : SingleTagBlockParser<RequestBlock>
{
    public const string OpenTag = "<request>";

    public const string CloseTag = "</request>";

    public RequestBlockParser() : base(OpenTag, CloseTag)
    {
        
    }

    protected override void PostProcess(RequestBlock block)
    {
        block.ContentBuilder.Append(block.Lines.ToString());
    }
}
