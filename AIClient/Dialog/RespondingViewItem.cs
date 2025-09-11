using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Windows.Documents;
using System.Windows.Input;
using LLMClient.Abstraction;
using LLMClient.Endpoints;
using LLMClient.Endpoints.Messages;
using LLMClient.UI;
using LLMClient.UI.Component;
using LLMClient.UI.Render;
using Microsoft.Xaml.Behaviors.Core;
using ChatFinishReason = Microsoft.Extensions.AI.ChatFinishReason;
using ChatMessage = Microsoft.Extensions.AI.ChatMessage;

namespace LLMClient.Dialog;

public class RespondingViewItem : BaseViewModel, IResponseViewItem
{
    public bool IsAvailableInContext { get; } = false;

    public ObservableCollection<string> RespondingText { get; } = new();

    public FlowDocument Document { get; set; } = new();

    private ILLMChatClient Client { get; }

    public ICommand CancelCommand => new ActionCommand(o => { RequestTokenSource.Cancel(); });

    public CancellationTokenSource RequestTokenSource { get; } = new CancellationTokenSource();

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

    public RespondingViewItem(ILLMChatClient client)
    {
        this.Client = client;
    }

    public async Task<CompletedResult> SendRequest(DialogContext context)
    {
        using (var blockingCollection = new BlockingCollection<string>())
        {
            var customRenderer = CustomRenderer.NewRenderer(Document);
            var task = Task.Run(() =>
            {
                // ReSharper disable once AccessToDisposedClosure
                RendererExtensions.StreamParse(blockingCollection,
                    (_, block) =>
                    {
                        Dispatch(() =>
                        {
                            RespondingText.Clear();
                            customRenderer.AppendMarkdownObject(block);
                        });
                    });
            });
            var completedResult = await Client.SendRequest(context,
                responseText =>
                {
                    blockingCollection.Add(responseText);
                    Dispatch(() => RespondingText.Add(responseText));
                },
                cancellationToken: this.RequestTokenSource.Token);
            blockingCollection.CompleteAdding();
            await task;
            return completedResult;
        }
    }

    public IAsyncEnumerable<ChatMessage> GetMessagesAsync(CancellationToken cancellationToken)
    {
        throw new NotSupportedException();
    }
}