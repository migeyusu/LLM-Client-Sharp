using LLMClient.Abstraction;
using Microsoft.Agents.AI;

namespace LLMClient.Dialog.Models;


public interface ISession
{
    /// <summary>
    /// session id
    /// </summary>
    Guid ID { get; }
    
    IResponseItem WorkingResponse { get; }

    IEnumerable<IDialogItem> GetChatHistory()
    {
        return WorkingResponse.GetDialogHistory();
    }

    AIContextProvider[]? ContextProviders { get; }
    
    IPromptCommandAggregate? PromptCommand { get; }

    string? SystemPrompt { get; }
}