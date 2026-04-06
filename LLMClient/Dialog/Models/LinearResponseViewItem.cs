using System.Collections.ObjectModel;
using System.Windows.Input;
using AutoMapper;
using CommunityToolkit.Mvvm.Input;
using LLMClient.Abstraction;
using LLMClient.Agent;
using LLMClient.Agent.MiniSWE;
using LLMClient.Component.CustomControl;
using LLMClient.Component.Utility;
using LLMClient.Endpoints;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Xaml.Behaviors.Core;

namespace LLMClient.Dialog.Models;

/// <summary>
/// 线性历史的ResponseViewItem
/// </summary>
public class LinearResponseViewItem : MultiResponseViewItem<RawResponseViewItem>
{
    public override long Tokens => Items.Sum(x => x.Tokens);

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
        get { return this.Items.Any(item => item.IsInterrupt); }
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

    public override IEnumerable<ChatMessage> Messages => Items.SelectMany(x => x.Messages);

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

    private ReactLoopViewModel? _currentLoop;

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
            return ChatCallResult.Empty;
        }

        var totalCallResult = new ChatCallResult();
        this.ErrorMessage = null;
        this.LoopCount = 0;
        this.Loops.Clear();
        this._currentLoop = null;
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
                await foreach (var callResult in Agent.Execute(session,
                                   cancellationToken: cancellationToken))
                {
                    var viewItem =
                        ServiceLocator.GetService<IMapper>()!.Map<IResponse, RawResponseViewItem>(callResult);
                    totalCallResult += callResult;
                    this.Items.Add(viewItem);

                    // 更新 loop 信息
                    LoopCount++;
                    var loop = new ReactLoopViewModel { LoopNumber = LoopCount };
                    Loops.Add(loop);
                    _currentLoop = loop;
                    loop.IsCompleted = true;
                    loop.IsExpanded = false;
                }

                ParentSession.OnResponseCompleted(totalCallResult);
            }
        }
        catch (Exception e)
        {
            MessageBoxes.Error(e.Message, "响应失败");
            this.ErrorMessage = e.Message;
            totalCallResult.Exception = e;
        }
        finally
        {
            this.IsResponding = false;
            OnPropertyChanged(nameof(IsInterrupt));
            OnPropertyChanged(nameof(IsAvailableInContext));
        }

        return totalCallResult;
    }
}