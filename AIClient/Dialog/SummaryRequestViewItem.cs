using System.Runtime.CompilerServices;
using LLMClient.UI;
using Microsoft.Extensions.AI;

namespace LLMClient.Dialog;

public class SummaryRequestViewItem : EraseViewItem, IRequestItem
{
    public int OutputLength { get; set; }

    public string? SummaryPrompt { get; set; }

    public override long Tokens
    {
        //估计tokens
        get => (long)((SummaryPrompt?.Length / 2.5) ?? 0);
    }

    public override async IAsyncEnumerable<ChatMessage> GetMessagesAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(SummaryPrompt))
        {
            throw new InvalidOperationException("SummaryPrompt cannot be null or empty.");
        }

        var message = new ChatMessage(ChatRole.User, SummaryPrompt);
        yield return message;
    }

    public override bool IsAvailableInContext { get; } = true;

    public static async Task<SummaryRequestViewItem> NewSummaryRequest()
    {
        var config = await GlobalOptions.LoadOrCreate();
        return new SummaryRequestViewItem()
        {
            SummaryPrompt = config.TokenSummarizePrompt,
            OutputLength = config.SummarizeWordsCount,
            InteractionId = Guid.NewGuid(),
        };
    }

    public Guid InteractionId { get; set; }
}