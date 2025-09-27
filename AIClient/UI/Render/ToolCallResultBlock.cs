using System.Xml.Serialization;
using Markdig.Parsers;

namespace LLMClient.UI.Render;

public class ToolCallResultBlock : CustomLeafBlock
{
    public ToolCallResultBlock(BlockParser parser) : base(parser)
    {
    }

    public ToolCallResultsContainer? ToolCallResults { get; set; }

    public string? Raw { get; set; }
}

[XmlRoot("tool_call_results")]
public class ToolCallResultsContainer
{
    [XmlElement("tool_call_result")]
    public List<ToolCallResultElement> ToolCalls { get; set; } = new List<ToolCallResultElement>();
}

public class ToolCallResultElement
{
    [XmlElement("name")] public string Name { get; set; } = string.Empty;

    [XmlElement("result")] public string ResultContent { get; set; } = string.Empty;
}