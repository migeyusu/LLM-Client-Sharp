using System.Collections.Specialized;
using System.Windows.Input;
using LLMClient.Abstraction;
using LLMClient.UI.Component;
using Microsoft.Extensions.AI;
using Microsoft.Xaml.Behaviors.Core;

namespace LLMClient.UI;

public class RespondingViewItem : BaseViewModel, IResponseViewItem
{
    public Task<ChatMessage?> GetMessage()
    {
        throw new NotSupportedException();
    }

    public bool IsAvailableInContext { get; } = false;

    public ILLMModelClient Client { get; }

    public ICommand CancelCommand => new ActionCommand(o => { RequestTokenSource.Cancel(); });

    public CancellationTokenSource RequestTokenSource { get; } = new CancellationTokenSource();

    public RespondingViewItem(ILLMModelClient client)
    {
        this.Client = client;
    }

    public ThemedIcon Icon
    {
        get { return Client.Model.Icon; }
    }

    public string ModelName
    {
        get { return Client.Model.Name; }
    }

    public string EndPointName
    {
        get { return Client.Endpoint.Name; }
    }

    public long Tokens { get; } = 0;

    public int Latency { get; set; } = 0;
    public int Duration { get; } = 0;
    public string? Raw { get; } = string.Empty;

    public bool IsInterrupt { get; } = false;

    public string? ErrorMessage { get; } = string.Empty;

    public double? Price { get; } = 0;
}