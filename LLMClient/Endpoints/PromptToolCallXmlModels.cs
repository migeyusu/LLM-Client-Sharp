using System.Xml.Serialization;

namespace LLMClient.Endpoints;

public class ToolCallElement
{
    [XmlElement("name")] public string Name { get; set; } = string.Empty;

    [XmlElement("arguments")] public string Arguments { get; set; } = string.Empty;
}

[XmlRoot("tool_calls")]
public class ToolCallsElement
{
    [XmlElement("tool_call")] public List<ToolCallElement> ToolCalls { get; set; } = [];
}

[XmlRoot("tool_call_results")]
public class ToolCallResultsContainer
{
    [XmlElement("tool_call_result")]
    public List<ToolCallResultElement> ToolCalls { get; set; } = [];
}

public class ToolCallResultElement
{
    [XmlElement("name")] public string Name { get; set; } = string.Empty;

    [XmlElement("result")] public string ResultContent { get; set; } = string.Empty;
}

