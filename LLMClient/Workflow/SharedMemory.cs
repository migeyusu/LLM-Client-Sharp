namespace LLMClient.Workflow;

/// <summary>
/// 关键点：这个类通过引用在各个 Agent 之间传递，并支持序列化保存到磁盘
/// 工作流上下文 (Blackboard Pattern)
/// </summary>
public class SharedMemory
{
    public string CurrentPlan { get; set; } = string.Empty;
    // 存储生成的代码Diff或完整代码，Key是文件路径
    public Dictionary<string, string> PendingCodeChanges { get; set; } = new();
    public List<string> ReviewComments { get; set; } = new();
}