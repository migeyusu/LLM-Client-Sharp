using Microsoft.Extensions.AI;

namespace LLMClient.Dialog.Models;



/// <summary>
/// 线性历史的ResponseViewItem
/// </summary>
public class LinearHistoryViewItem : BaseDialogItem, IResponseItem
{
    public Guid InteractionId { get; set; }

    private long _tokensCount = 0;

    public override long Tokens => _tokensCount;

    public override ChatRole Role => ChatRole.System;

    public ResponseViewItem Type { get; set; }

    public override IAsyncEnumerable<ChatMessage> GetMessagesAsync(CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public override bool IsAvailableInContext { get; }
}