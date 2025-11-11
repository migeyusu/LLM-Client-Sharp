using Microsoft.Extensions.AI;

namespace LLMClient.ToolCall;

public sealed class FunctionAuthorizationInterceptor : IFunctionInterceptor
{
    public static readonly FunctionAuthorizationInterceptor Instance = new FunctionAuthorizationInterceptor();

    public List<IFunctionAuthorizationFilter> Filters { get; } = new List<IFunctionAuthorizationFilter>();

    public async Task<object?> InvokeAsync(AIFunction function, AIFunctionArguments arguments,
        FunctionCallContent content, CancellationToken token)
    {
        foreach (var filter in Filters)
        {
            if (filter.Matches(content))
            {
                if (!await filter.AuthorizeAsync(content, token))
                {
                    throw new UnauthorizedAccessException("You are not authorized to execute this function.");
                }
            }
        }

        //do nothing, just invoke the function
        return await function.InvokeAsync(arguments, token);
    }
}