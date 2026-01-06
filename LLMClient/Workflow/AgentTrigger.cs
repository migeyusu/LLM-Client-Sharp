namespace LLMClient.Workflow;

/// <summary>
/// 定义触发状态转换的信号
/// </summary>
public enum AgentTrigger
{
    Start,
    InspectComplete,
    PlanApproved,
    CodeGenerated,
    CodeRejected,   // 比如编译失败或Review没过
    CodeApproved,
    WriteComplete,
    CriticalError,
    Reset
}