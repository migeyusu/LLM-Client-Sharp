using System.Text.Json.Serialization;
using LLMClient.Data;
using Microsoft.Extensions.AI;

namespace LLMClient.UI.Dialog;

public class EraseViewItem : IDialogItem, IDialogPersistItem
{
    public virtual IAsyncEnumerable<ChatMessage> GetMessages(CancellationToken cancellationToken)
    {
        throw new NotSupportedException();
    }

    [JsonIgnore] public virtual bool IsAvailableInContext { get; } = false;

    public virtual long Tokens { get; } = 0;
}