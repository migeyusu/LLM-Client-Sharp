using Microsoft.Extensions.AI;

namespace LLMClient.Abstraction;

public interface IMcpServiceCollection : IEnumerable<IAIFunctionGroup>
{
    IEnumerable<AITool> AvailableTools { get; }

    bool IsInitialized { get; }

    Task RefreshToolsAsync();
    Task LoadAsync();
}