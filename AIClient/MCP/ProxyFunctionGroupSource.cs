using LLMClient.Abstraction;
using LLMClient.MCP.Servers;

namespace LLMClient.MCP;

public class ProxyFunctionGroupSource : IFunctionGroupSource
{
    private readonly Func<IEnumerable<IAIFunctionGroup>?> _functionGroupsFunc;

    public ProxyFunctionGroupSource(Func<IEnumerable<IAIFunctionGroup>?> functionGroupsFunc)
    {
        _functionGroupsFunc = functionGroupsFunc;
    }

    public IEnumerable<IAIFunctionGroup> GetFunctionGroups()
    {
        return _functionGroupsFunc.Invoke() ?? [];
    }
}