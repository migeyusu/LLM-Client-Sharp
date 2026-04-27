using LLMClient.Abstraction;
using LLMClient.Endpoints;
using LLMClient.ToolCall;
using Microsoft.Extensions.AI;

namespace LLMClient.Dialog.Models;

/// <summary>
/// 用于总结请求的对话项，替代原有的 EraseViewItem + RequestViewItem 组合。
/// 通过状态开关控制 GetChatHistory 的遍历行为：
/// - 正在总结时：作为普通请求包含在历史中
/// - 总结完成后：作为截断边界，阻止继续向前遍历
/// - 总结失败时：跳过该项，继续向前遍历
/// </summary>
public class SummaryRequestViewItem : BaseDialogItem, IContextBoundaryItem, IRequestItem
{
    private readonly ChatMessage _chatMessage;

    public SummaryRequestViewItem(IRequestItem sourceRequest)
    {
        var text = sourceRequest.UserPrompt ?? string.Empty;
        _chatMessage = new ChatMessage(ChatRole.User, text);
        InteractionId = sourceRequest.InteractionId;
        State = SummaryRequestState.Summarizing;
    }

    public SummaryRequestViewItem(string rawTextMessage, DialogSessionViewModel? parentSession = null)
    {
        _chatMessage = new ChatMessage(ChatRole.User, rawTextMessage);
        InteractionId = Guid.NewGuid();
        State = SummaryRequestState.Summarizing;
        Session = parentSession;
    }

    /// <summary>
    /// 总结请求的状态
    /// </summary>
    public SummaryRequestState State
    {
        get;
        set
        {
            if (value == field) return;
            field = value;
            OnPropertyChanged();
        }
    }

    public Guid InteractionId { get; set; }

    public override long Tokens => 0;

    public override DialogRole Role => DialogRole.Summary;

    public override IDialogSession? Session { get; }

    public override bool IsAvailableInContext => State == SummaryRequestState.Summarizing;

    public void TriggerTextContentUpdate()
    {
        throw new NotSupportedException();
    }

    public override IEnumerable<ChatMessage> Messages
    {
        get { yield return _chatMessage; }
    }

    public ContextBoundaryEvaluation EvaluateHistoryBoundary(Guid? interactionId)
    {
        return State switch
        {
            SummaryRequestState.Summarizing => ContextBoundaryEvaluation.IncludeAndContinue(interactionId),
            SummaryRequestState.Completed => ContextBoundaryEvaluation.Stop(interactionId),
            SummaryRequestState.Failed => ContextBoundaryEvaluation.Continue(null),
            _ => ContextBoundaryEvaluation.Stop(interactionId)
        };
    }

    public string? UserPrompt => null;

    public ISearchOption? SearchOption => null;
    public List<CheckableFunctionGroupTree>? FunctionGroups => null;

    public IRagSource[]? RagSources => null;

    public ChatResponseFormat? ResponseFormat => null;

    public FunctionCallEngineType CallEngineType => FunctionCallEngineType.Prompt;

    public AdditionalPropertiesDictionary? TempAdditionalProperties => null;

    public bool IsDebugMode => false;

    public bool AutoApproveAllInvocations => true;
}

public enum SummaryRequestState
{
    /// <summary>
    /// 正在总结中，GetChatHistory 将此项视为普通请求
    /// </summary>
    Summarizing,

    /// <summary>
    /// 总结完成，GetChatHistory 将此项视为截断边界
    /// </summary>
    Completed,

    /// <summary>
    /// 总结失败，GetChatHistory 将跳过此项
    /// </summary>
    Failed
}