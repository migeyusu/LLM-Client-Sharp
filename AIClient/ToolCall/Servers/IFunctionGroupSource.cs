using LLMClient.Abstraction;

namespace LLMClient.ToolCall.Servers;

public interface IFunctionGroupSource
{
    IEnumerable<IAIFunctionGroup> GetFunctionGroups();
}