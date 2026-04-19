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

    public override ChatRole Role => ChatRole.Assistant;

    public override IEnumerable<ChatMessage> Messages => Response.Messages;

    public IAgent? Agent { get; }

    /// <summary>
    /// True when the agent supports history compaction (any <see cref="ReactAgentBase"/> subclass).
    /// Used by XAML to show/hide the compact menu item.
    /// </summary>
    public bool IsCompactable => Agent is ReactAgentBase;

    public IAsyncRelayCommand SmartEliminateHistoryCommand { get; }

    public ICommand EliminateFailedHistoryCommand { get; }

    public ICommand EliminateHistoryCommand { get; }

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

    public bool CanEliminate
    {
        get
        {
            if (!IsResponding && Response.IsInterrupt)
            {
                return false;
            }

            return true;
        }
    }

    public LinearResponseViewItem(DialogSessionViewModel parentSession, IAgent? agent,
        RawResponseViewItem? response = null)
    {
        ParentSession = parentSession;
        Agent = agent;
        Response = response ?? new RawResponseViewItem();
        SmartEliminateHistoryCommand = new AsyncRelayCommand(
            CompactHistoryAsync, () => Agent is ReactAgentBase && !IsResponding && Response.Messages.Any());
        EliminateHistoryCommand = new RelayCommand((() =>
            {
                if (Response.Messages.Count() <= 1)
                {
                    return;
                }

                if (Agent is not PlannerAgent)
                {
                    return;
                }

                if (!CanEliminate)
                {
                    return;
                }

                //只保留最后一条消息
                Response.Messages = [Response.Messages.Last()];
            }),
            () => Agent is PlannerAgent && !IsResponding && Response.Messages.Any());
        EliminateFailedHistoryCommand = new RelayCommand((() =>
        {
            if (Agent is not ReactAgentBase) return;
            if (!Response.Messages.Any())
            {
                return;
            }

            if (!CanEliminate)
            {
                return;
            }

            try
            {
                var messages = Response.Messages.ToList();
                var segmentation = ReactHistorySegmenter.Segment(messages);
                var keptMessages = new List<ChatMessage>(segmentation.PreambleMessages);

                foreach (var round in segmentation.Rounds)
                {
                    var functionCalls = round.AssistantMessages
                        .SelectMany(m => m.Contents.OfType<FunctionCallContent>())
                        .ToList();

                    if (functionCalls.Count == 0)
                    {
                        keptMessages.AddRange(round.Messages);
                        continue;
                    }

                    var resultDict = round.ObservationMessages
                        .SelectMany(m => m.Contents.OfType<FunctionResultContent>())
                        .ToDictionary(r => r.CallId);

                    var allFailed = functionCalls.All(call =>
                        resultDict.TryGetValue(call.CallId, out var result) && result.Exception != null);

                    if (!allFailed)
                    {
                        keptMessages.AddRange(round.Messages);
                    }
                }

                Response.Messages = keptMessages;
                Response.InvalidateAsyncProperty(nameof(RawResponseViewItem.FullDocument));
                SmartEliminateHistoryCommand.NotifyCanExecuteChanged();
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[EliminateFailedHistory Error]: {ex.Message}");
                MessageBoxes.Error(ex.Message, "清除失败历史失败");
            }
        }), () => Agent is ReactAgentBase && !IsResponding && Response.Messages.Any());
    }

    private async Task CompactHistoryAsync(CancellationToken cancellationToken)
    {
        if (Agent is not ReactAgentBase reactAgent) return;
        if (!Response.Messages.Any())
        {
            return;
        }

        if (!CanEliminate)
        {
            return;
        }

        IsResponding = true;
        ParentSession.RespondingCount++;
        try
        {
            var compactor = new HistoryCompactor(reactAgent.ChatClient)
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

                Response.InvalidateAsyncProperty(nameof(RawResponseViewItem.FullDocument));
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
            using (Response.CreateRequestTokenSource(token, out var liveToken))
            {
                await ParentSession.OnPreviewRequest(liveToken);
                agentTaskResult = await Response.ConsumeReactStepsAsync(
                    Agent.Execute(session, liveToken));
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
        }

        return agentTaskResult;
    }
}