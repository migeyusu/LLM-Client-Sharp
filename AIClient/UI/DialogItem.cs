using System.Text.Json.Serialization;
using LLMClient.Data;
using Microsoft.Extensions.AI;

namespace LLMClient.UI;

public interface ITokenizable
{
    /// <summary>
    /// （估计的）tokens数量
    /// </summary>
    long Tokens { get; }
}

public interface IDialogViewItem : ITokenizable
{
    Task<ChatMessage?> GetMessage();

    bool IsAvailableInContext { get; }
}

public class EraseViewItem : IDialogViewItem, IDialogPersistItem
{
    public Task<ChatMessage?> GetMessage()
    {
        return Task.FromResult<ChatMessage?>(null);
    }
    [JsonIgnore]
    public bool IsAvailableInContext { get; } = false;

    public long Tokens { get; } = 0;
}