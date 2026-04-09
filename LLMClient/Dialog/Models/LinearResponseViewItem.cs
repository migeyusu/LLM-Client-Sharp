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

    public IAgent? Agent { get; }

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
    }

    public async Task<IResponse> ProcessAsync(ITextDialogSession session, CancellationToken token)
    {
        if (Agent == null)
        {
            MessageBoxes.Error("No agent configured.");
            return AgentTaskResult.Empty;
        }

        IsResponding = true;
        try
        {
            Response.RequestTokenSource = Response.CreateRequestTokenSource(token);
            using (Response.RequestTokenSource)
            {
                var cancellationToken = Response.RequestTokenSource.Token;
                await ParentSession.OnPreviewRequest(cancellationToken);
                var totalCallResult = await Response.ConsumeReactStepsAsync(
                    Agent.Execute(session, cancellationToken: cancellationToken));
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
            IsResponding = false;
            Response.InvalidateAsyncProperty(nameof(RawResponseViewItem.FullDocument));
        }
    }
}