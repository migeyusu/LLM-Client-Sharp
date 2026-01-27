using Microsoft.Extensions.AI;

namespace LLMClient.Dialog;

public class RootDialogItem : BaseDialogItem
{
    public static RootDialogItem Instance { get; } = new();

    private static readonly ChatRole EmptyRole = new ChatRole("Empty");
    public override long Tokens { get; } = 0;
    public override ChatRole Role { get; } = EmptyRole;
    public override string DisplayText { get; } = "Root";

    public override IAsyncEnumerable<ChatMessage> GetMessagesAsync(CancellationToken cancellationToken)
    {
        throw new NotSupportedException();
    }

    public override bool IsAvailableInContext { get; } = false;
}