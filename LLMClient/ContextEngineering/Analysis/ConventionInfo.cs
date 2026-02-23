namespace LLMClient.ContextEngineering.Analysis;

public sealed class ConventionInfo
{
    public bool HasEditorConfig { get; set; }
    public string? EditorConfigPath { get; set; }

    public bool UsesNullable { get; set; }
    public bool UsesImplicitUsings { get; set; }

    public string? DefaultNamespaceStyle { get; set; } // e.g. "Company.Product.Module"
    public string? TestFrameworkHint { get; set; }     // xUnit/nUnit/MSTest/Unknown

    public List<string> NotableFiles { get; set; } = new(); // README/ADR/etc (仅项目感知范围)
}