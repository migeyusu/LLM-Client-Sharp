namespace LLMClient.Workflow;

/// <summary>
/// Agent 执行后的结果
/// </summary>
public class AgentExecutionResult
{
    // 是否成功执行
    public bool Success { get; set; }
    
    // 决定下一个状态的 Trigger (由 Agent 内部逻辑决定)
    // 比如 Reviewer 决定是 Trigger.CodeApproved 还是 Trigger.CodeRejected
    public AgentTrigger NextTrigger { get; set; }
    
    // 给 UI 或日志的消息
    public string OutputMessage { get; set; } = string.Empty;
    
    // 可选：携带某些元数据
    public object? ResultData { get; set; }

    public static AgentExecutionResult FromTrigger(AgentTrigger trigger, string msg) 
        => new() { Success = true, NextTrigger = trigger, OutputMessage = msg };
        
    public static AgentExecutionResult Error(string error) 
        => new() { Success = false, NextTrigger = AgentTrigger.CriticalError, OutputMessage = error };
}
