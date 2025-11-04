using Microsoft.Extensions.AI;

namespace LLMClient.ToolCall;

public interface IFunctionAuthorizationFilter
{
    bool Matches(FunctionCallContent functionCall);

    Task<bool> AuthorizeAsync(FunctionCallContent functionCall, CancellationToken cancellationToken);
}
