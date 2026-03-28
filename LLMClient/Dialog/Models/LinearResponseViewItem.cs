using System.Collections.ObjectModel;
using System.Windows.Input;
using AutoMapper;
using CommunityToolkit.Mvvm.Input;
using LLMClient.Abstraction;
using LLMClient.Agent;
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
public class LinearResponseViewItem : MultiResponseViewItem<RawResponseViewItem>, IInvokeInteractor
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
        get;
        set
        {
            if (value == field) return;
            field = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsAvailableInContext));
        }
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

    public ObservableCollection<string> History { get; } = [];

    public ObservableCollection<AsyncPermissionViewModel> PermissionViewModels { get; } = [];

    public IAgent? Agent { get; }

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
                await foreach (var callResult in Agent.Execute(session, interactor: this,
                                   cancellationToken: cancellationToken))
                {
                    var viewItem =
                        ServiceLocator.GetService<IMapper>()!.Map<IResponse, RawResponseViewItem>(callResult);
                    totalCallResult += callResult;
                    this.Items.Add(viewItem);
                    this.History.Clear();
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
            this.History.Clear();
            this.PermissionViewModels.Clear();
            this.IsResponding = false;
            this.IsInterrupt = totalCallResult.IsInterrupt;
        }

        return totalCallResult;
    }


    public void Info(string message)
    {
        History.Add(message);
    }

    public void Error(string message)
    {
        History.Add(message);
    }

    public void Warning(string message)
    {
        History.Add(message);
    }

    public void Write(string message)
    {
        History.Add(message);
    }

    public void WriteLine(string? message = null)
    {
        History.AddLine(message);
    }

    public Task<bool> WaitForPermission(string title, string message)
    {
        return InvokePermissionDialog.RequestAsync(title, message);
    }

    public Task<bool> WaitForPermission(object content)
    {
        return InvokePermissionDialog.RequestAsync(content);
    }
}