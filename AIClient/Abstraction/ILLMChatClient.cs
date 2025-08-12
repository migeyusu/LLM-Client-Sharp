using System.Collections.ObjectModel;
using LLMClient.Endpoints;
using LLMClient.MCP;

namespace LLMClient.Abstraction;

public interface ILLMChatClient
{
    /// <summary>
    /// 唯一名称
    /// </summary>
    string Name { get; }

    ILLMEndpoint Endpoint { get; }

    ILLMChatModel Model { get; }

    bool IsResponding { get; set; }

    IModelParams Parameters { get; set; }

    IFunctionInterceptor FunctionInterceptor { get; set; }

    ObservableCollection<string> RespondingText { get; }

    Task<CompletedResult> SendRequest(DialogContext context,
        CancellationToken cancellationToken = default);
}