using System.Collections.ObjectModel;
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
public class LinearResponseViewItem : MultiResponseViewItem<RawResponseViewItem>
{
    private readonly List<ChatMessage> _accumulatedMessages = [];
    private UsageDetails? _accumulatedUsage;
    private bool _hasError;

    public override long Tokens => _accumulatedUsage?.OutputTokenCount ?? Items.Sum(x => x.Tokens);

    public override bool IsAvailableInContext
    {
        get { return (IsManualValid || !IsInterrupt) && IsAvailableInContextSwitch; }
    }

    /// <summary>
    /// 可以通过手动控制实现叠加的上下文可用性
    /// </summary>
    public bool IsAvailableInContextSwitch { get; set; } = true;

    /// <summary>
    /// 手动标记为有效 
    /// </summary>
    public bool IsManualValid
    {
        get;
        set
        {
            if (value == field) return;
            field = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsAvailableInContext));
        }
    } = false;

    public bool IsInterrupt
    {
        get { return _hasError || this.Items.Any(item => item.IsInterrupt); }
    }

    public string? ErrorMessage
    {
        get;
        set
        {
            if (value == field) return;
            field = value;
            OnPropertyChanged();
        }
    }

    public override bool IsResponding
    {
        get;
        protected set
        {
            if (value == field) return;
            field = value;
            OnPropertyChanged();
        }
    }

    public override ChatRole Role => ChatRole.Assistant;

    public override IEnumerable<ChatMessage> Messages =>
        _accumulatedMessages.Count > 0 ? _accumulatedMessages : Items.SelectMany(x => x.Messages);

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
    /// 每轮 ReAct 循环的 ViewModel 列表
    /// </summary>
    public ObservableCollection<ReactLoopViewModel> Loops { get; } = [];


    public IAgent? Agent { get; }

    /// <summary>
    /// 模型的最大上下文长度（用于计算上下文占比）
    /// </summary>
    public int? MaxContextTokens => (Agent as MiniSweAgent)?.ChatClient?.Model?.MaxContextSize;

    public CancellationTokenSource? RequestTokenSource { get; private set; }

    public ICommand CancelCommand { get; }

    public ICommand SetAsAvailableCommand { get; }

    public LinearResponseViewItem(IEnumerable<RawResponseViewItem> items, DialogSessionViewModel parentSession,
        IAgent? agent) : base(items, parentSession)
    {
        Agent = agent;
        CancelCommand = new ActionCommand(o => { RequestTokenSource?.Cancel(); });
        SetAsAvailableCommand = new ActionCommand(o =>
        {
            if (!IsManualValid && IsInterrupt)
            {
                MessageEventBus.Publish("无法切换中断的响应，请先标记为有效");
                return;
            }

            IsAvailableInContextSwitch = !IsAvailableInContextSwitch;
        });
    }

    public LinearResponseViewItem(DialogSessionViewModel parentSession, IAgent? agent) : this([], parentSession, agent)
    {
    }

    public async Task<IResponse> ProcessAsync(ITextDialogSession session, CancellationToken token)
    {
        if (Agent == null)
        {
            MessageBoxes.Error("No agent configured.");
            return AgentTaskResult.Empty;
        }

        this.ErrorMessage = null;
        this._accumulatedMessages.Clear();
        this._accumulatedUsage = null;
        this._hasError = false;
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
                    MaxContextTokens);

                _accumulatedMessages.AddRange(totalCallResult.Messages);
                _accumulatedUsage = totalCallResult.Usage;
                _hasError = totalCallResult.Exception != null;

                ParentSession.OnResponseCompleted(totalCallResult);
                return totalCallResult;
            }
        }
        catch (Exception e)
        {
            MessageBoxes.Error(e.Message, "响应失败");
            this.ErrorMessage = e.Message;
            return new AgentTaskResult { Exception = e };
        }
        finally
        {
            this.IsResponding = false;
            OnPropertyChanged(nameof(IsInterrupt));
            OnPropertyChanged(nameof(IsAvailableInContext));
        }
    }
}