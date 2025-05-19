using System.Windows.Media;
using LLMClient.Abstraction;
using LLMClient.Data;
using LLMClient.UI.Component;
using Microsoft.Extensions.AI;

namespace LLMClient.UI;

public class RespondingViewItem : BaseViewModel, IResponseViewItem
{
    public ChatMessage? Message { get; } = null;
    public bool IsAvailableInContext { get; } = false;

    public ILLMModelClient Client { get; }

    public RespondingViewItem(ILLMModelClient client)
    {
        this.Client = client;
    }

    public ThemedIcon Icon
    {
        get { return Client.Info.Icon; }
    }

    public string ModelName
    {
        get { return Client.Info.Name; }
    }

    public long Tokens { get; } = 0;
}