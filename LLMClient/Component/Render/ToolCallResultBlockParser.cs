using System.Diagnostics;
using System.Xml.Serialization;

namespace LLMClient.Component.Render;

public class ToolCallResultBlockParser : SingleTagBlockParser<ToolCallResultBlock>
{
    public const string FunctionResultTag = "<tool_call_results>";

    public const string FunctionResultEndTag = "</tool_call_results>";

    public ToolCallResultBlockParser() : base(FunctionResultTag, FunctionResultEndTag)
    {
    }

    protected override void PostProcess(ToolCallResultBlock block)
    {
        var content = block.Lines.ToString().Trim();

        try
        {
            var full = $"{FunctionResultTag}\n{content}\n{FunctionResultEndTag}";
            var xmlSerializer = new XmlSerializer(typeof(ToolCallResultsContainer));
            using var stringReader = new StringReader(full);
            if (xmlSerializer.Deserialize(stringReader) is ToolCallResultsContainer container)
            {
                block.ToolCallResults = container;
            }
        }
        catch (Exception e)
        {
            block.Raw = content;
            Trace.TraceWarning("Failed to parse tool call results: {0}", e.Message);
        }
    }
}