using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using LLMClient.Data;
using Microsoft.Extensions.AI;

namespace LLMClient.UI.Dialog;

public class EraseViewItem : IDialogItem, IDialogPersistItem
{
    public async IAsyncEnumerable<ChatMessage> GetMessages([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        yield break;
    }

    [JsonIgnore] public bool IsAvailableInContext { get; } = false;

    public long Tokens { get; } = 0;
}