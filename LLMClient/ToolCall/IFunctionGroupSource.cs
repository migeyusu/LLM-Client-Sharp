using LLMClient.Abstraction;

namespace LLMClient.ToolCall;

public interface IFunctionGroupSource
{
    IEnumerable<IAIFunctionGroup> GetFunctionGroups();
}