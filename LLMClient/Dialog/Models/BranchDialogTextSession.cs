using LLMClient.Abstraction;
using LLMClient.ToolCall;
using Microsoft.Agents.AI;

namespace LLMClient.Dialog.Models;

/// <summary>
/// 為父對話項產生的分支
/// </summary>
public class BranchDialogTextSession : ITextDialogSession
{
    public ITextDialogSession ParentSession { get; }

    public BranchDialogTextSession(ITextDialogSession parentSession, IResponseItem responseItem)
    {
        this.ParentSession = parentSession;
        this.VisualDialogItems = responseItem.GetChatHistory().ToArray();
        WorkingResponse = responseItem;
    }

    public static BranchDialogTextSession CreateFromResponse(IResponseItem responseItem)
    {
        return new BranchDialogTextSession(
            responseItem.Session ??
            throw new NotSupportedException("ResponseItem must have a session to create BranchDialogTextSession."),
            responseItem);
    }

    public Guid ID => ParentSession.ID;

    public IReadOnlyList<IDialogItem> VisualDialogItems { get; }

    public IResponseItem WorkingResponse { get; }

    public string? WorkingDirectory => ParentSession.WorkingDirectory;

    public Task CutContextAsync(IRequestItem? requestItem = null)
    {
        throw new NotSupportedException(
            "BranchDialogTextSession does not support cutting context. Please cut context in the parent session.");
    }

    public AIContextProvider[]? ContextProviders => ParentSession.ContextProviders;

    public IPromptCommandAggregate? PromptCommand => ParentSession.PromptCommand;

    public string? SystemPrompt => null;

    public IEnumerable<Type> SupportedAgents => ParentSession.SupportedAgents;

    public IFunctionGroupSource? ToolsSource => ParentSession.ToolsSource;

    public Task<IResponse> NewResponse(RequestOption option, IRequestItem? insertBefore = null,
        CancellationToken token = default)
    {
        throw new NotSupportedException();
    }
}