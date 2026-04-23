using LLMClient.Abstraction;
using LLMClient.ToolCall;
using Microsoft.Agents.AI;

namespace LLMClient.Dialog.Models;

/// <summary>
/// 為父對話項產生的分支
/// </summary>
public class BranchDialogTextSession : ITextDialogSession
{
    private readonly ITextDialogSession _parentSession;

    public BranchDialogTextSession(ITextDialogSession parentSession, IResponseItem responseItem)
    {
        this._parentSession = parentSession;
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

    public Guid ID => _parentSession.ID;

    public IReadOnlyList<IDialogItem> VisualDialogItems { get; private set; }

    public IResponseItem WorkingResponse { get; private set; }

    public Task CutContextAsync(IRequestItem? requestItem = null)
    {
        throw new NotSupportedException(
            "BranchDialogTextSession does not support cutting context. Please cut context in the parent session.");
    }

    public AIContextProvider[]? ContextProviders => null;

    public string? SystemPrompt => null;

    public IEnumerable<Type> SupportedAgents => _parentSession.SupportedAgents;

    public IFunctionGroupSource? ToolsSource => _parentSession.ToolsSource;

    public Task<IResponse> NewResponse(RequestOption option, IRequestItem? insertBefore = null,
        CancellationToken token = default)
    {
        throw new NotSupportedException();
    }
}