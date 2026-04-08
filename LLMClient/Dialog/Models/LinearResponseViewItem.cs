using System.ComponentModel;
using System.Windows.Input;
using AutoMapper;
using LLMClient.Abstraction;
using LLMClient.Agent;
using LLMClient.Agent.MiniSWE;
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
        }
    }

    public override ChatRole Role => ChatRole.Assistant;

    public override IEnumerable<ChatMessage> Messages => Response.Messages;

    /// <summary>
    /// 响应过程中当前正在执行的操作（状态事件描述）
    /// </summary>
    public string? CurrentStatus
    {
        get;
        private set
        {
            if (value == field) return;
            field = value;
            OnPropertyChanged();
        }
    }

    public IAgent? Agent { get; }

    /// <summary>
    /// 模型的最大上下文长度（用于计算上下文占比）
    /// </summary>
    public int? MaxContextTokens => Agent is MiniSweAgent miniSweAgent
        ? miniSweAgent.ChatClient.Model.MaxContextSize
        : null;

    public CancellationTokenSource? RequestTokenSource
    {
        get { return Response.RequestTokenSource; }
        private set { Response.RequestTokenSource = value; }
    }

    public ICommand CancelCommand { get; }

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
        CancelCommand = new ActionCommand(_ => ResponseViewItemBase.CancelRequest(RequestTokenSource));
    }

    public async Task<IResponse> ProcessAsync(ITextDialogSession session, CancellationToken token)
    {
        if (Agent == null)
        {
            MessageBoxes.Error("No agent configured.");
            return AgentTaskResult.Empty;
        }

        Response.LoopCount = 0;
        CurrentStatus = null;
        IsResponding = true;
        try
        {
            RequestTokenSource = token != CancellationToken.None
                ? CancellationTokenSource.CreateLinkedTokenSource(token)
                : new CancellationTokenSource();
            using (RequestTokenSource)
            {
                var cancellationToken = RequestTokenSource.Token;
                await ParentSession.OnPreviewRequest(cancellationToken);
                var totalCallResult = await Response.ConsumeReactStepsAsync(
                    Agent.Execute(session, cancellationToken: cancellationToken),
                    cancellationToken,
                    MaxContextTokens,
                    status => Dispatch(() => CurrentStatus = status));
                ServiceLocator.GetService<IMapper>()!.Map<IResponse, ResponseViewItemBase>(totalCallResult, Response);
                ParentSession.OnResponseCompleted(totalCallResult);
                return totalCallResult;
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
            return Response;
        }
        finally
        {
            RequestTokenSource = null;
            IsResponding = false;
            Response.InvalidateAsyncProperty(nameof(RawResponseViewItem.FullDocument));
        }
    }
}