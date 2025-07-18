using LLMClient.Abstraction;

namespace LLMClient.UI.MCP.Servers;

public interface IFunctionGroupSource
{
    IEnumerable<IAIFunctionGroup> GetFunctionGroups();
}