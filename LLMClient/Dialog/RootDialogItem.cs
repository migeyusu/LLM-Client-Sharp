using Microsoft.Extensions.AI;

namespace LLMClient.Dialog;

/// <summary>
/// 空的根对话项，作为对话树的根节点。
/// </summary>
public class RootDialogItem : BaseDialogItem
{
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