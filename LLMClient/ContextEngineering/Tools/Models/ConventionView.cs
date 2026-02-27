namespace LLMClient.ContextEngineering.Tools.Models;

public sealed class ConventionView
{
    public bool HasEditorConfig { get; set; }
    public string? EditorConfigPath { get; set; }

    public bool UsesNullable { get; set; }
    public bool UsesImplicitUsings { get; set; }

    public string? DefaultNamespaceStyle { get; set; }
    public string? TestFrameworkHint { get; set; }

    public List<string> NotableFiles { get; set; } = new();
}