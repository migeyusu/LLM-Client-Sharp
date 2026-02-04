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

    public override ChatRole Role { get; } = EraseRole;

    static ChatRole EraseRole = new ChatRole("erase");
}