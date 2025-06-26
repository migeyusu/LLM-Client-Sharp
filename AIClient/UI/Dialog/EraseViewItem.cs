using System.Text.Json.Serialization;
using LLMClient.Data;
using Microsoft.Extensions.AI;

namespace LLMClient.UI.Dialog;

public class EraseViewItem : IDialogItem, IDialogPersistItem
{
    public Task<ChatMessage?> GetMessage()
    {
        return Task.FromResult<ChatMessage?>(null);
    }
    [JsonIgnore]
    public bool IsAvailableInContext { get; } = false;

    public long Tokens { get; } = 0;
}