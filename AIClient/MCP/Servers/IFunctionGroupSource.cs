using LLMClient.Abstraction;

namespace LLMClient.MCP.Servers;

public interface IFunctionGroupSource
{
    IEnumerable<IAIFunctionGroup> GetFunctionGroups();
}