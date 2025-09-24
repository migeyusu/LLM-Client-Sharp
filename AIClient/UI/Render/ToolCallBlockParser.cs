using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml.Serialization;

namespace LLMClient.UI.Render;

public class ToolCallBlockParser : SingleTagBlockParser<ToolCallBlock>
{
    public const string FunctionCallTag = "<tool_calls>";

    public const string FunctionCallEndTag = "</tool_calls>";

    public ToolCallBlockParser() : base(FunctionCallTag, FunctionCallEndTag)
    {
    }

    protected override void PostProcess(ToolCallBlock block)
    {
        var content = block.Lines.ToString().Trim();
        // parse xml content
        try
        {
            var full = $"{FunctionCallTag}\n{content}\n{FunctionCallEndTag}";
            var xmlSerializer = new XmlSerializer(typeof(ToolCallsElement));
            using var reader = new StringReader(full);
            var toolCallsElement = xmlSerializer.Deserialize(reader) as ToolCallsElement;
            if (toolCallsElement != null)
            {
                block.ToolCalls = toolCallsElement;
                
            }
        }
        catch (Exception exception)
        {
            block.Raw = content;
            Trace.TraceWarning("Failed to parse tool calls: {0}", exception.Message);
        }
    }
}