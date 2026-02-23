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
        var value = block.Lines.ToString();
        block.ContentBuilder.Append(value);
    }
}
