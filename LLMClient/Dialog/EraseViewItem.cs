using System.Text.Json.Serialization;
using LLMClient.Data;
using Microsoft.Extensions.AI;

namespace LLMClient.Dialog;

public class EraseViewItem : BaseDialogItem
{
    public override IAsyncEnumerable<ChatMessage> GetMessagesAsync(CancellationToken cancellationToken)
    {
        throw new NotSupportedException();
    }

    public override bool IsAvailableInContext => false;

    public override long Tokens => 0;
}