using System.ComponentModel;
using System.Diagnostics;
using System.Windows.Input;
using AutoMapper;
using CommunityToolkit.Mvvm.Input;
using LLMClient.Abstraction;
using LLMClient.Agent;
using LLMClient.Agent.Planner;
using LLMClient.Component.CustomControl;
using LLMClient.Endpoints;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Xaml.Behaviors.Core;

namespace LLMClient.Dialog.Models;

/// <summary>
/// 线性历史的ResponseViewItem
/// </summary>
public class LinearResponseViewItem : BaseDialogItem, IResponseItem
{
    public RawResponseViewItem Response { get; }

    public override long Tokens => Response.Tokens;

    public override IDialogSession? Session => ParentSession;

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
            SmartEliminateHistoryCommand.NotifyCanExecuteChanged();
        }
    }

    public override DialogRole Role => DialogRole.Response;

    public override IEnumerable<ChatMessage> Messages => Response.Messages;

    public IAgent? Agent { get; }

    /// <summary>
    /// True when the agent supports history compaction (any <see cref="ReactAgentBase"/> subclass).
    /// Used by XAML to show/hide the compact menu item.
    /// </summary>
    public bool IsCompactable => Agent is ReactAgentBase;

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

    public IAsyncRelayCommand SmartEliminateHistoryCommand { get; }

    public LinearResponseViewItem(DialogSessionViewModel parentSession, IAgent? agent,
        RawResponseViewItem? response = null)
    {
        ParentSession = parentSession;
        Agent = agent;
        Response = response ?? new RawResponseViewItem();
        SmartEliminateHistoryCommand = new AsyncRelayCommand(
            CompactHistoryAsync, () => !IsResponding && Messages.Any());
    }

    private async Task CompactHistoryAsync(CancellationToken cancellationToken)
    {
        if (Agent is not ReactAgentBase reactAgent) return;
        if (!Response.Messages.Any())
        {
            return;
        }

        if (!Response.CanEliminate)
        {
            return;
        }

        IsResponding = true;
        ParentSession.RespondingCount++;
        try
        {
            var compactor = new HistoryPruner(reactAgent.ChatClient)
            {
                ErrorTag = "HistoryCompact",
            };

            //last message is inspect or plan
            var roundMessages = Response.Messages.SkipLast(1)
                .ToArray();
            using (Response.CreateRequestTokenSource(cancellationToken, out var liveToken))
            {
                var compacted = await compactor.CompactAsync(
                    FindPrecedingTask(),
                    ParentSession.SystemPrompt,
                    roundMessages,
                    liveToken);
                if (compacted != null)
                {
                    compacted.Add(Response.Messages.Last());
                    Response.Messages = compacted;
                }

                Response.InvalidateAsyncProperty(nameof(RawResponseViewItem.SearchableDocument));
            }
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
        var items = ParentSession.VisualDialogItems;
        var idx = items.IndexOf(this);
        for (var i = idx - 1; i >= 0; i--)
        {
            if (items[i] is RequestViewItem req)
                return req.RawTextMessage;
        }

        return null;
    }

    public async Task<IResponse> ProcessAsync(CancellationToken token)
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
            using (Response.CreateRequestTokenSource(token, out var liveToken))
            {
                await ParentSession.OnPreviewRequest(liveToken);
                var branchDialogTextSession = BranchDialogTextSession.CreateFromResponse(this);
                agentTaskResult = await Response.ConsumeReactStepsAsync(
                    Agent.Execute(branchDialogTextSession, liveToken));
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
            Response.InvalidateAsyncProperty(nameof(RawResponseViewItem.SearchableDocument));
            ParentSession.RespondingCount--;
        }

        return agentTaskResult;
    }
}