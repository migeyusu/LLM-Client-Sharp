using System.Reflection;
using System.Text;
using System.Text.Json;
using LLMClient.Abstraction;
using LLMClient.Agent;
using LLMClient.Dialog;
using LLMClient.Endpoints;
using Microsoft.Extensions.AI;

namespace LLMClient.Workflow.Dynamic;

public class WorkflowArchitect : PromptBasedAgent, IAgentStep
{
    private readonly IEnumerable<IAgentStep> _availableAgents;

    public WorkflowArchitect(ILLMChatClient chatClient, IEnumerable<IAgentStep> agents, IInvokeInteractor? interactor)
        : base(chatClient, interactor)
    {
        _availableAgents = agents;
    }

    //todo: fixe this method
    public async Task<WorkflowBlueprint> PlanAsync(string userRequest)
    {
        // 1. 构建 System Prompt：列出所有可用工具
        var toolsDesc = BuildToolsDescription();

        var prompt = $@"
You are a Workflow Architect. The user has a request: '{userRequest}'.
Available Agents:
{toolsDesc}

Plan a sequential workflow to solve the user's request.
Return ONLY valid JSON defined as:
{{
  ""goalSummary"": ""string"",
  ""steps"": [ {{ ""id"": 1, ""agentName"": ""AgentName"", ""specificInstruction"": ""detail"" }} ]
}}
";

        // 2. 调用 LLM (建议开启 JSON Mode)
        var response = await this.SendRequestAsync(new DialogContext(new[]
        {
            new RequestViewItem(prompt)
            {
                ResponseFormat = ChatResponseFormat.ForJsonSchema<WorkflowBlueprint>()
            },
        }));

        // 3. 反序列化
        return JsonSerializer.Deserialize<WorkflowBlueprint>(response.FirstTextResponse,
            Extension.DefaultJsonSerializerOptions);
    }

    private string BuildToolsDescription()
    {
        var sb = new StringBuilder();
        foreach (var agent in _availableAgents)
        {
            var attr = agent.GetType().GetCustomAttribute<AgentCapabilityAttribute>();
            if (attr != null)
            {
                sb.AppendLine($"- Name: {attr.Name}");
                sb.AppendLine($"  Desc: {attr.Description}");
            }
        }

        return sb.ToString();
    }

    public AgentState TargetState { get; } = AgentState.Planning;

    public Task<AgentExecutionResult> ExecuteAsync(WorkflowContext context)
    {
        throw new NotImplementedException();
    }
}