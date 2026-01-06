namespace LLMClient.Workflow;

public class WorkflowContext
{
    public Guid Id { get; init; } = Guid.NewGuid();
    
    // 用户的原始需求
    public string UserPrompt { get; set; } = string.Empty;
    
    // Roslyn AST 上下文的精简引用 (不要直接存 Roslyn 对象，存路径或 Symbol ID)
    public List<string> TargetFilePaths { get; set; } = new();
    
    // 用于 Agent 之间传递的中间产物
    public SharedMemory Memory { get; set; } = new();
    
    // 执行日志（用于 UI 渲染时间轴）
    public List<WorkflowStepLog> ExecutionHistory { get; set; } = new();
    
    // 错误信息快照
    public string? LastErrorMessage { get; set; }
    
    // 控制标志
    public CancellationToken CancellationToken { get; set; }
}