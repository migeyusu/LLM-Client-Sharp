using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using LLMClient.Abstraction;
using LLMClient.Agent;
using LLMClient.Agent.MiniSWE;
using LLMClient.Component.CustomControl;
using LLMClient.Component.Utility;
using LLMClient.Endpoints;
using Microsoft.Extensions.AI;
using Microsoft.Xaml.Behaviors.Core;

namespace LLMClient.Dialog.Models;

/// <summary>
/// 线性历史的ResponseViewItem
/// </summary>
public class LinearResponseViewItem : BaseDialogItem, IResponseItem
{
    [Bindable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public RawResponseViewItem Response { get; }

    public override long Tokens => Response.Tokens;

    public override bool IsAvailableInContext
    {
        get { return (IsManualValid || !IsInterrupt) && IsAvailableInContextSwitch; }
    }

    /// <summary>
    /// 可以通过手动控制实现叠加的上下文可用性
    /// </summary>
    public bool IsAvailableInContextSwitch
    {
        get { return field; }
        set
        {
            if (value == field) return;
            field = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsAvailableInContext));
        }
    } = true;

    /// <summary>
    /// 手动标记为有效 
    /// </summary>
    public bool IsManualValid
    {
        get { return field; }
        set
        {
            if (value == field) return;
            field = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsAvailableInContext));
        }
    }

    public bool IsInterrupt
    {
        get { return Response.IsInterrupt; }
    }

    public bool IsResponding
    {
        get { return field; }
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

    public int LoopCount
    {
        get;
        set
        {
            if (value == field) return;
            field = value;
            OnPropertyChanged();
        }
    }

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

    /// <summary>
    /// 每轮 ReAct 循环的 ViewModel 列表
    /// </summary>
    public ObservableCollection<ReactLoopViewModel> Loops { get; } = [];
    
    public IAgent? Agent { get; }

    /// <summary>
    /// 模型的最大上下文长度（用于计算上下文占比）
    /// </summary>
    public int? MaxContextTokens => Agent is MiniSweAgent miniSweAgent
        ? miniSweAgent.ChatClient.Model.MaxContextSize
        : null;

    public CancellationTokenSource? RequestTokenSource { get; private set; }

    public ICommand CancelCommand { get; }

    public ICommand SetAsAvailableCommand { get; }

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

    public LinearResponseViewItem(DialogSessionViewModel parentSession, IAgent? agent, RawResponseViewItem? response = null)
    {
        ParentSession = parentSession;
        Agent = agent;
        Response = response ?? new RawResponseViewItem();
        Response.PropertyChanged += OnResponsePropertyChanged;
        CancelCommand = new ActionCommand(_ =>
        {
            RequestTokenSource?.Cancel();
        });
        SetAsAvailableCommand = new ActionCommand(_ =>
        {
            if (!IsManualValid && IsInterrupt)
            {
                MessageEventBus.Publish("无法切换中断的响应，请先标记为有效");
                return;
            }

            IsAvailableInContextSwitch = !IsAvailableInContextSwitch;
        });
    }

    public async Task<IResponse> ProcessAsync(ITextDialogSession session, CancellationToken token)
    {
        if (Agent == null)
        {
            MessageBoxes.Error("No agent configured.");
            return AgentTaskResult.Empty;
        }

        ResetResponse();
        this.IsResponding = true;
        try
        {
            RequestTokenSource = token != CancellationToken.None
                ? CancellationTokenSource.CreateLinkedTokenSource(token)
                : new CancellationTokenSource();
            using (RequestTokenSource)
            {
                var cancellationToken = RequestTokenSource.Token;
                await ParentSession.OnPreviewRequest(cancellationToken);
                var totalCallResult = await ResponseViewItemBase.ConsumeReactStepsAsync(
                    Agent.Execute(session, cancellationToken: cancellationToken),
                    Loops,
                    count => LoopCount = count,
                    cancellationToken,
                    MaxContextTokens,
                    status => Dispatch(() => CurrentStatus = status));

                ApplyResponse(totalCallResult);
                Response.InvalidateAsyncProperty(nameof(RawResponseViewItem.FullDocument));

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
            return new AgentTaskResult { Exception = e };
        }
        finally
        {
            this.IsResponding = false;
            OnPropertyChanged(nameof(Tokens));
            OnPropertyChanged(nameof(Messages));
            Response.RawTextContent = null;
            Response.InvalidateAsyncProperty(nameof(RawResponseViewItem.FullDocument));
            OnPropertyChanged(nameof(IsInterrupt));
            OnPropertyChanged(nameof(IsAvailableInContext));
        }
    }

    private void ResetResponse()
    {
        Response.ErrorMessage = null;
        Response.Usage = null;
        Response.LastSuccessfulUsage = null;
        Response.Messages = [];
        Response.FinishReason = null;
        Response.Annotations = null;
        Response.IsInterrupt = false;
        Response.RawTextContent = null;
        Response.IsResponding = false;
        Response.InvalidateAsyncProperty(nameof(RawResponseViewItem.FullDocument));
        CurrentStatus = null;
        OnPropertyChanged(nameof(Messages));
        OnPropertyChanged(nameof(Tokens));
        OnPropertyChanged(nameof(IsInterrupt));
        OnPropertyChanged(nameof(IsAvailableInContext));
    }

    private void OnResponsePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(RawResponseViewItem.Usage):
            case nameof(RawResponseViewItem.Tokens):
                OnPropertyChanged(nameof(Tokens));
                break;
            case nameof(RawResponseViewItem.Messages):
                Response.InvalidateAsyncProperty(nameof(RawResponseViewItem.FullDocument));
                OnPropertyChanged(nameof(Messages));
                break;
            case nameof(RawResponseViewItem.IsInterrupt):
                OnPropertyChanged(nameof(IsInterrupt));
                OnPropertyChanged(nameof(IsAvailableInContext));
                break;
        }
    }

    private void ApplyResponse(IResponse response)
    {
        Response.Price = response.Price;
        Response.Usage = response.Usage;
        Response.LastSuccessfulUsage = response.LastSuccessfulUsage;
        Response.Messages = response.Messages;
        Response.Latency = response.Latency;
        Response.Duration = response.Duration;
        Response.ErrorMessage = response.ErrorMessage;
        Response.Annotations = response.Annotations;
        Response.FinishReason = response.FinishReason;
        Response.IsInterrupt = response.IsInterrupt;
        Response.RawTextContent = null;
    }
}