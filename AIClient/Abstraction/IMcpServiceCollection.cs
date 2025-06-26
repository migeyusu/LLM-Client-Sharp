using Microsoft.Extensions.AI;

namespace LLMClient.Abstraction;

public interface IMcpServiceCollection
{
    IEnumerable<AITool> AvailableTools { get; }

    Task RefreshAsync();
    Task LoadAsync();
}