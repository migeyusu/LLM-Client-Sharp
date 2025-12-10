using System.Xml.Serialization;
using Markdig.Parsers;

namespace LLMClient.Component.Render;

public class ToolCallBlock : CustomLeafBlock
{
    public ToolCallBlock(BlockParser parser) : base(parser)
    {
    }


    public ToolCallsElement? ToolCalls { get; set; }

    public string? Raw { get; set; }
}

public class ToolCallElement
{
    [XmlElement("name")] public string Name { get; set; } = string.Empty;

    [XmlElement("arguments")] public string Arguments { get; set; } = string.Empty;
}

[XmlRoot("tool_calls")]
public class ToolCallsElement
{
    [XmlElement("tool_call")] public List<ToolCallElement> ToolCalls { get; set; } = new List<ToolCallElement>();
}