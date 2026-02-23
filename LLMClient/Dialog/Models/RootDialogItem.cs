using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

namespace LLMClient.Dialog.Models;

/// <summary>
/// 空的根对话项，作为对话树的根节点。
/// </summary>
public class RootDialogItem : BaseDialogItem
{
    private static readonly ChatRole EmptyRole = new ChatRole("Empty");
    public override long Tokens { get; } = 0;
    public override ChatRole Role { get; } = EmptyRole;

    public override async IAsyncEnumerable<ChatMessage> GetMessagesAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        yield break;
    }

    public override bool IsAvailableInContext { get; } = false;
}