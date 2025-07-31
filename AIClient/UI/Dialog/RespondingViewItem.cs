using System.Windows.Input;
using LLMClient.Abstraction;
using LLMClient.Endpoints.Messages;
using LLMClient.UI.Component;
using Microsoft.Xaml.Behaviors.Core;
using ChatFinishReason = Microsoft.Extensions.AI.ChatFinishReason;
using ChatMessage = Microsoft.Extensions.AI.ChatMessage;

namespace LLMClient.UI.Dialog;

public class RespondingViewItem : BaseViewModel, IResponseViewItem
{
    public IAsyncEnumerable<ChatMessage> GetMessages(CancellationToken cancellationToken)
    {
        throw new NotSupportedException();
    }

    public bool IsAvailableInContext { get; } = false;

    public ILLMClient Client { get; }

    public ICommand CancelCommand => new ActionCommand(o => { RequestTokenSource.Cancel(); });

    public CancellationTokenSource RequestTokenSource { get; } = new CancellationTokenSource();

    public RespondingViewItem(ILLMClient client)
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

    public bool IsInterrupt { get; } = false;

    public string? ErrorMessage { get; } = string.Empty;

    public double? Price { get; } = 0;
    public IList<ChatMessage>? ResponseMessages { get; } = null;

    public IList<ChatAnnotation>? Annotations { get; set; } = null;

    public ChatFinishReason? FinishReason { get; set; }
}