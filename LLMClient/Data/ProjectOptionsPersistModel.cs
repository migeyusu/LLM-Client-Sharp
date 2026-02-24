using System.Text.Json.Serialization;
using LLMClient.Project;

namespace LLMClient.Data;

public class ProjectOptionsPersistModel
{
    public string? Name { get; set; }

    public string? Description { get; set; }

    public ProjectType? Type { get; set; }

    [JsonPropertyName("FolderPath")]
    public string? RootPath { get; set; }

    public string[]? AllowedFolderPaths { get; set; }
}