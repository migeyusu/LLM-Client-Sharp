using System.Collections.ObjectModel;
using AutoMapper;
using LLMClient.Abstraction;
using LLMClient.Agent;
using LLMClient.Endpoints;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace LLMClient.Dialog.Models;

/// <summary>
/// 只支持简单原始文本，用于agent任务
/// </summary>
public class RawResponseViewItem : ResponseViewItemBase
{
    public IEnumerable<ChatMessage> GetMessages()
    {
        return this.Messages ?? [];
    }
}

/// <summary>
/// 线性历史的ResponseViewItem
/// </summary>
public class LinearHistoryResponseViewItem : MultiResponseViewItem<RawResponseViewItem>, IInvokeInteractor
{
    private long _tokensCount = 0;
    public override long Tokens => _tokensCount;

    public override bool IsAvailableInContext { get; } = true;

    public ObservableCollection<string> History { get; set; } = [];

    public ObservableCollection<AsyncPermissionViewModel> PermissionViewModels { get; set; } = [];

    private readonly IAgent _agent;
    public IAgent Agent => _agent;

    public LinearHistoryResponseViewItem(IEnumerable<RawResponseViewItem> items, DialogSessionViewModel parentSession,
        IAgent agent) : base(items, parentSession)
    {
        _agent = agent;
    }

    public LinearHistoryResponseViewItem(DialogSessionViewModel parentSession, IAgent agent) : base([], parentSession)
    {
        _agent = agent;
    }

    public async Task ProcessAsync(DialogContext context, CancellationToken token)
    {
        await foreach (var callResult in _agent.Execute(context, this, token))
        {
            var viewItem = ServiceLocator.GetService<IMapper>()!.Map<IResponse, RawResponseViewItem>(callResult);
            this.Items.Add(viewItem);
            History.Clear();
        }
    }

    public override IEnumerable<ChatMessage> Messages
    {
        get { return this.Items.SelectMany(responseViewItemBase => responseViewItemBase.Messages); }
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

    public async Task<bool> WaitForPermission(string title, string message)
    {
        var permissionViewModel = new AsyncPermissionViewModel() { Title = title, Content = message };
        PermissionViewModels.Add(permissionViewModel);
        var result = await permissionViewModel.Task;
        PermissionViewModels.Remove(permissionViewModel);
        return result;
    }

    public async Task<bool> WaitForPermission(object content)
    {
        var vm = new AsyncPermissionViewModel { Content = content };
        PermissionViewModels.Add(vm);
        var result = await vm.Task;
        PermissionViewModels.Remove(vm);
        return result;
    }
}