using System.Reflection;
using LLMClient.Abstraction;
using LLMClient.MCP.Servers;

namespace LLMClient.Research;

public class ResearchModelService : IResearchModelService
{
    public ResearchModelService()
    {
        AvailableResearchModels = new[] { "NV Research" };
    }

    public IReadOnlyList<string> AvailableResearchModels { get; }

    public ILLMChatClient CreateResearchClient(string modelName, ILLMChatClient client)
    {
        return new NvidiaResearchClient(client, new GoogleSearchPlugin());
    }
}