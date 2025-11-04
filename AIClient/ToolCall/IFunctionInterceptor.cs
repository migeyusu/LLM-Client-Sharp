using Microsoft.Extensions.AI;
using FunctionCallContent = Microsoft.Extensions.AI.FunctionCallContent;

namespace LLMClient.ToolCall;

public interface IFunctionInterceptor
{
    ///  <summary>  
    /// Intercepts the function call 
    /// </summary>
    ///  <param name="function">The function will be called.</param>
    ///  <param name="arguments">The arguments passed to the function.</param>
    ///  <param name="content">The content of the function call.</param>
    ///  <param name="token"></param>
    ///  <returns>A task that represents the asynchronous operation, containing the response from the function call.</returns>
    Task<object?> InvokeAsync(AIFunction function, AIFunctionArguments arguments,
        FunctionCallContent content, CancellationToken token);
}