using LLMClient.Abstraction;
using LLMClient.Endpoints;
using LLMClient.ToolCall;
using Microsoft.Extensions.AI;

namespace LLMClient.Dialog.Models;

[global::System.Obsolete("Legacy compatibility type. Runtime summary flow now uses EraseViewItem + RequestViewItem + SummaryAgent.")]
public class SummaryRequestViewItem : BaseDialogItem, IRequestItem, IContextBoundaryItem
{
    public int OutputLength { get; set; }

    public string? SummaryPrompt { get; set; }

    public bool IsSummarizing { get; set; }

    public override long Tokens
    {
        //估计tokens
        get => (long)(SummaryPrompt?.Length / 2.8 ?? 0);
    }

    public override IEnumerable<ChatMessage> Messages
    {
        get
        {
            if (string.IsNullOrEmpty(SummaryPrompt))
            {
                throw new InvalidOperationException("SummaryPrompt cannot be null or empty.");
            }

            yield return new ChatMessage(ChatRole.User, SummaryPrompt);
        }
    }

    public override bool IsAvailableInContext { get; } = true;

    public override ChatRole Role { get; } = ChatRole.User;

    public void TriggerTextContentUpdate()
    {
    }

    public Guid InteractionId { get; set; }

    public string? UserPrompt
    {
        get
        {
            return SummaryPrompt;
        }
    }

    public ISearchOption? SearchOption { get; } = null;

    public List<CheckableFunctionGroupTree>? FunctionGroups { get; } = null;

    public IRagSource[]? RagSources { get; } = null;

    public ChatResponseFormat? ResponseFormat { get; } = null;

    public FunctionCallEngineType CallEngineType { get; } = FunctionCallEngineType.Prompt;

    public AdditionalPropertiesDictionary? TempAdditionalProperties { get; } = null;

    public bool IsDebugMode { get; } = true;

    public bool AutoApproveAllInvocations { get; set; }

    public ContextBoundaryEvaluation EvaluateHistoryBoundary(Guid? interactionId)
    {
        if (IsSummarizing)
        {
            return ContextBoundaryEvaluation.IncludeAndContinue(interactionId);
        }

        return InteractionId == interactionId
            ? ContextBoundaryEvaluation.Stop(interactionId)
            : ContextBoundaryEvaluation.Continue(null);
    }
}