namespace LLMClient.Dialog.Models;

public interface IContextBoundaryItem
{
    ContextBoundaryEvaluation EvaluateHistoryBoundary(Guid? interactionId);
}

public readonly record struct ContextBoundaryEvaluation(
    bool IncludeInHistory,
    bool StopTraversal,
    Guid? NextInteractionId)
{
    public static ContextBoundaryEvaluation Continue(Guid? nextInteractionId)
    {
        return new ContextBoundaryEvaluation(false, false, nextInteractionId);
    }

    public static ContextBoundaryEvaluation IncludeAndContinue(Guid? nextInteractionId)
    {
        return new ContextBoundaryEvaluation(true, false, nextInteractionId);
    }

    public static ContextBoundaryEvaluation Stop(Guid? nextInteractionId = null)
    {
        return new ContextBoundaryEvaluation(false, true, nextInteractionId);
    }
}

