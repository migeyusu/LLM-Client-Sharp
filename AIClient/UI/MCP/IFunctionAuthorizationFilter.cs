using Microsoft.Extensions.AI;

namespace LLMClient.UI.MCP;

public interface IFunctionAuthorizationFilter
{
    bool Matches(FunctionCallContent functionCall);

    Task<bool> AuthorizeAsync(FunctionCallContent functionCall, CancellationToken cancellationToken);
}
