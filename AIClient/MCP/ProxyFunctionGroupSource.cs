using LLMClient.Abstraction;
using LLMClient.UI.MCP.Servers;

namespace LLMClient.UI.MCP;

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