using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

namespace LLMClient.Dialog.Models;

/// <summary>
/// 空的根对话项，作为对话树的根节点。
/// </summary>
public class RootDialogItem : BaseDialogItem
{
    public override long Tokens { get; } = 0;

    public override DialogRole Role { get; } = DialogRole.None;

    public override IEnumerable<ChatMessage> Messages
    {
        get { yield break; }
    }

    public override IDialogSession? Session { get; } = null;

    public override bool IsAvailableInContext { get; } = false;
}