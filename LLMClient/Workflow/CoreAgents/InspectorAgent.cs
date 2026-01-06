using LLMClient.Abstraction;
using LLMClient.Agent;
using LLMClient.Endpoints;

namespace LLMClient.Workflow.CoreAgents;
 
public class InspectorAgent : PromptBasedAgent, IAgentStep
{
    public InspectorAgent(ILLMChatClient chatClient, IInvokeInteractor? interactor) : base(chatClient, interactor)
    {
    }

    public AgentState TargetState => AgentState.Inspecting;

    public async Task<AgentExecutionResult> ExecuteAsync(WorkflowContext context)
    {
        // 1. 使用 Roslyn 分析 SLN/CSPROJ
        // 2. 提取相关的 AST (Methods, Referneces)
        // 3. 将关键信息存入 context.Memory
        
        await Task.Delay(1000); // 模拟耗时
        return AgentExecutionResult.FromTrigger(
            AgentTrigger.InspectComplete, 
            "Analyzed AST, found 3 related classes.");
    }
}