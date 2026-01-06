using LLMClient.Abstraction;
using LLMClient.Agent;
using LLMClient.Endpoints;
using LLMClient.Workflow.Dynamic;

namespace LLMClient.Workflow.CoreAgents;

[AgentCapability("Reviewer", "编译现有代码并运行测试。如果失败会抛出错误。")]
public class ReviewerAgent :PromptBasedAgent, IAgentStep
{
    public ReviewerAgent(ILLMChatClient chatClient, IInvokeInteractor? interactor) : base(chatClient, interactor)
    {
    }

    public AgentState TargetState => AgentState.Reviewing;

    public async Task<AgentExecutionResult> ExecuteAsync(WorkflowContext context)
    {
        // 1. 尝试编译 (Roslyn Compilation with pending changes)
        // 2. 运行单元测试
        // 3. 甚至再调用一次 LLM 让它 Review 代码风格
        
        bool buildSuccess = CheckCompilation(context.Memory.PendingCodeChanges);

        if (buildSuccess)
        {
            return AgentExecutionResult.FromTrigger(
                AgentTrigger.CodeApproved, 
                "Compilation passed, ready to write.");
        }
        else
        {
            // 添加错误信息到 Memory，以便下一轮 Coder 看到错误进行修正
            context.Memory.ReviewComments.Add("Error CS1002: ; expected at line 42");
            
            // 关键：触发拒绝，状态机回退到 Coding
            return AgentExecutionResult.FromTrigger(
                AgentTrigger.CodeRejected, 
                "Compilation failed, requesting fix.");
        }
    }
    
    private bool CheckCompilation(Dictionary<string, string> codes) => true; // Stub
}