using System.ComponentModel;
using System.Diagnostics;
using AutoMapper;
using CommunityToolkit.Mvvm.Input;
using LLMClient.Abstraction;
using LLMClient.Agent;
using LLMClient.Component.CustomControl;
using LLMClient.Endpoints;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace LLMClient.Dialog.Models;

/// <summary>
/// 线性历史的ResponseViewItem
/// </summary>
public class LinearResponseViewItem : BaseDialogItem, IResponseItem
{
    public RawResponseViewItem Response { get; }

    public override long Tokens => Response.Tokens;

    [Bindable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public override bool IsAvailableInContext
    {
        get { return Response.IsAvailableInContext; }
    }

    public bool IsResponding
    {
        get;
        private set
        {
            if (value == field) return;
            field = value;
            Response.IsResponding = value;
            OnPropertyChanged();
            CompactHistoryCommand.NotifyCanExecuteChanged();
        }
    }

    public override ChatRole Role => ChatRole.Assistant;

    public override IEnumerable<ChatMessage> Messages => Response.Messages;

    public IAgent? Agent { get; }

    /// <summary>
    /// True when the agent supports history compaction (any <see cref="ReactAgentBase"/> subclass).
    /// Used by XAML to show/hide the compact menu item.
    /// </summary>
    public bool IsCompactable => Agent is ReactAgentBase;

    public IAsyncRelayCommand CompactHistoryCommand { get; }

    public Guid InteractionId
    {
        get;
        set
        {
            if (value.Equals(field)) return;
            field = value;
            OnPropertyChanged();
        }
    }

    public DialogSessionViewModel ParentSession { get; }

    public LinearResponseViewItem(DialogSessionViewModel parentSession, IAgent? agent,
        RawResponseViewItem? response = null)
    {
        ParentSession = parentSession;
        Agent = agent;
        Response = response ?? new RawResponseViewItem();
        CompactHistoryCommand = new AsyncRelayCommand(
            CompactHistoryAsync,
            () => Agent is ReactAgentBase && !IsResponding && Response.Messages.Any());
    }

    private async Task CompactHistoryAsync(CancellationToken cancellationToken)
    {
        if (Agent is not ReactAgentBase reactAgent) return;

        IsResponding = true;
        ParentSession.RespondingCount++;
        try
        {
            var compactor = new HistoryCompactor(reactAgent.ChatClient)
            {
                ErrorTag = "HistoryCompact",
            };

            var roundMessages = Response.Messages
                .Select(m => (IReadOnlyList<ChatMessage>)[m])
                .ToList();

            var compacted = await compactor.CompactAsync(
                FindPrecedingTask(),
                ParentSession.SystemPrompt,
                roundMessages,
                cancellationToken);

            Response.Messages = compacted;
            Response.InvalidateAsyncProperty(nameof(RawResponseViewItem.FullDocument));
        }
        catch (OperationCanceledException)
        {
            // User cancelled — no action needed
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[HistoryCompact Error]: {ex.Message}");
            MessageBoxes.Error(ex.Message, "压缩失败");
        }
        finally
        {
            IsResponding = false;
            ParentSession.RespondingCount--;
        }
    }

    private string? FindPrecedingTask()
    {
        var items = ParentSession.DialogItems;
        var idx = items.IndexOf(this);
        for (var i = idx - 1; i >= 0; i--)
        {
            if (items[i] is RequestViewItem req)
                return req.RawTextMessage;
        }

        return null;
    }

    public async Task<IResponse> ProcessAsync(ITextDialogSession session, CancellationToken token)
    {
        var agentTaskResult = AgentTaskResult.Empty;
        if (Agent == null)
        {
            MessageBoxes.Error("No agent configured.");
            return agentTaskResult;
        }

        IsResponding = true;
        ParentSession.RespondingCount++;
        try
        {
            Response.RequestTokenSource = Response.CreateRequestTokenSource(token);
            using (Response.RequestTokenSource)
            {
                var cancellationToken = Response.RequestTokenSource.Token;
                await ParentSession.OnPreviewRequest(cancellationToken);
                agentTaskResult = await Response.ConsumeReactStepsAsync(
                    Agent.Execute(session, cancellationToken: cancellationToken));
                ParentSession.OnResponseCompleted(agentTaskResult);
            }
        }
        catch (OperationCanceledException)
        {
            return AgentTaskResult.Empty;
        }
        catch (Exception e)
        {
            MessageBoxes.Error(e.Message, "响应失败");
            Response.ErrorMessage = e.Message;
            Response.IsInterrupt = true;
            return agentTaskResult;
        }
        finally
        {
            IsResponding = false;
            ServiceLocator.GetService<IMapper>()!.Map<IResponse, ResponseViewItemBase>(agentTaskResult, Response);
            Response.InvalidateAsyncProperty(nameof(RawResponseViewItem.FullDocument));
            ParentSession.RespondingCount--;
            Response.RequestTokenSource = null;
        }

        return agentTaskResult;
    }
}