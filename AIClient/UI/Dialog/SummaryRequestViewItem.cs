using System.Runtime.CompilerServices;
using LLMClient.Data;
using Microsoft.Extensions.AI;

namespace LLMClient.UI.Dialog;

public class SummaryRequestViewItem : IRequestItem, IDialogPersistItem
{
    public int OutputLength { get; set; }

    public string? SummaryPrompt { get; set; }

    public long Tokens
    {
        //估计tokens
        get => (long)((SummaryPrompt?.Length / 2.5) ?? 0);
    }

    public async IAsyncEnumerable<ChatMessage> GetMessages([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(SummaryPrompt))
        {
            throw new InvalidOperationException("SummaryPrompt cannot be null or empty.");
        }

        var message = new ChatMessage(ChatRole.User, SummaryPrompt);
        yield return message;
    }

    public bool IsAvailableInContext { get; } = true;

    public static SummaryRequestViewItem NewSummaryRequest()
    {
        var config = GlobalConfig.LoadOrCreate();
        return new SummaryRequestViewItem()
        {
            SummaryPrompt = config.TokenSummarizePrompt,
            OutputLength = config.SummarizeWordsCount,
            InteractionId = Guid.NewGuid(),
        };
    }

    public Guid InteractionId { get; set; }
}