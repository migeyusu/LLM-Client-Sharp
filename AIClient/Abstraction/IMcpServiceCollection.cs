using Microsoft.Extensions.AI;

namespace LLMClient.Abstraction;

public interface IMcpServiceCollection : IEnumerable<IAIFunctionGroup>
{
    bool IsInitialized { get; }

    bool IsLoaded { get; }

    Task InitializeToolsAsync();

    Task LoadAsync();

    Task EnsureAsync();
}