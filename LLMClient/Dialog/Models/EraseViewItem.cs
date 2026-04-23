using Microsoft.Extensions.AI;

namespace LLMClient.Dialog.Models;

public class EraseViewItem : BaseDialogItem, IContextBoundaryItem
{
    public override IEnumerable<ChatMessage> Messages => throw new NotSupportedException();

    public override ITextDialogSession? Session { get; } = null;
    public override bool IsAvailableInContext => false;

    public override long Tokens => 0;

    public override DialogRole Role { get; } = DialogRole.Erase;

    public ContextBoundaryEvaluation EvaluateHistoryBoundary(Guid? interactionId)
    {
        return ContextBoundaryEvaluation.Stop(interactionId);
    }
}