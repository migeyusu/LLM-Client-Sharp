using System.Xml.Serialization;

namespace LLMClient.Project;

/// <summary>
/// 项目信息根节点
/// </summary>
[XmlRoot("project_information")]
public class ProjectInformation
{
    [XmlElement("project")]
    public ProjectInfo Project { get; set; } = new();

    [XmlElement("agents_rules")]
    public AgentsRules? AgentsRules { get; set; }
}

/// <summary>
/// 项目基本信息
/// </summary>
public class ProjectInfo
{
    [XmlElement("name")]
    public string Name { get; set; } = string.Empty;

    [XmlElement("type")]
    public string Type { get; set; } = string.Empty;

    [XmlElement("path")]
    public string Path { get; set; } = string.Empty;

    [XmlElement("description")]
    public string? Description { get; set; }
}

/// <summary>
/// AGENTS.md 内容
/// </summary>
public class AgentsRules
{
    [XmlText]
    public string Content { get; set; } = string.Empty;
}
