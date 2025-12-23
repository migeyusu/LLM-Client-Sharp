namespace LLMClient.ContextEngineering;

public class NamespaceInfo
{
    public required string Name { get; set; }
    public List<TypeInfo> Types { get; set; } = new();
    public required string FilePath { get; set; } // 追踪来源文件
}