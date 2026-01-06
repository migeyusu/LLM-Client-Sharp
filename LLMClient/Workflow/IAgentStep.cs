namespace LLMClient.Workflow;

/// <summary>
/// 核心接口：每个步骤（Planner, Coder）都是一个 Strategy
/// </summary>
public interface IAgentStep
{
    // 对应哪个状态
    AgentState TargetState { get; }
    
    // 真正的 LLM 调用逻辑、Roslyn 分析逻辑都在这里
    // 允许使用 Microsoft.Extensions.AI 或 Semantic Kernel
    Task<AgentExecutionResult> ExecuteAsync(WorkflowContext context);
}