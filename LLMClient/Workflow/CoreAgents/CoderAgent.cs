using LLMClient.Abstraction;
using LLMClient.Agent;
using LLMClient.Endpoints;
using LLMClient.Workflow.Dynamic;

namespace LLMClient.Workflow.CoreAgents;

[AgentCapability("CSharpCoder", "根据需求和计划编写 C# 代码。并在 Memory 中生成 Diff。")]
public class CoderAgent : PromptBasedAgent, IAgentStep
{
    public CoderAgent(ILLMChatClient chatClient, IInvokeInteractor? interactor) : base(chatClient, interactor)
    {
    }

    public AgentState TargetState => AgentState.Coding;

    public async Task<AgentExecutionResult> ExecuteAsync(WorkflowContext context)
    {
        // 1. 构建Prompt：Context.UserPrompt + Context.Memory.SharedAST
        // 2. 调用 LLM (Semantic Kernel)
        // 3. 解析 LLM 返回的 Markdown 代码块或 ToolCall
        // 4. 将代码存入 context.Memory.PendingCodeChanges (不直接写文件)

        // 假设这里有了代码
        bool hasError = false;

        if (hasError)
            return AgentExecutionResult.Error("LLM output invalid JSON.");

        return AgentExecutionResult.FromTrigger(
            AgentTrigger.CodeGenerated,
            "Generated implementation for IUserStore.");
    }
}