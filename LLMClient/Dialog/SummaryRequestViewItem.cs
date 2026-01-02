using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

namespace LLMClient.Dialog;

public class SummaryRequestViewItem : EraseViewItem, IRequestItem
{
    public int OutputLength { get; set; }

    public string? SummaryPrompt { get; set; }

    public override long Tokens
    {
        //估计tokens
        get => (long)(SummaryPrompt?.Length / 2.8 ?? 0);
    }

    public override async IAsyncEnumerable<ChatMessage> GetMessagesAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await Task.Yield();
        if (string.IsNullOrEmpty(SummaryPrompt))
        {
            throw new InvalidOperationException("SummaryPrompt cannot be null or empty.");
        }

        var message = new ChatMessage(ChatRole.User, SummaryPrompt);
        yield return message;
    }

    public override bool IsAvailableInContext { get; } = true;
    public void TriggerTextContentUpdate()
    {
        
    }

    public Guid InteractionId { get; set; }
}