using Microsoft.Extensions.AI;

namespace LLMClient.Agent.MiniSWE;

/// <summary>
/// 用于中断 Agent 流程并添加消息的异常基类
/// </summary>
public class InterruptAgentFlowException : Exception
{
    public List<ChatMessage> Messages { get; }

    public InterruptAgentFlowException(params ChatMessage[] messages)
    {
        Messages = messages.ToList();
    }

    public InterruptAgentFlowException(IEnumerable<ChatMessage> messages)
    {
        Messages = messages.ToList();
    }
}

/// <summary>
/// 任务已提交完成
/// </summary>
public class SubmittedException : InterruptAgentFlowException
{
    public string Submission { get; }

    public SubmittedException(string submission, ChatMessage exitMessage) : base(exitMessage)
    {
        Submission = submission;
    }
}

/// <summary>
/// 超出限制（步数或成本）
/// </summary>
public class LimitsExceededException : InterruptAgentFlowException
{
    public LimitsExceededException(ChatMessage exitMessage) : base(exitMessage)
    {
    }
}

/// <summary>
/// 格式错误（LLM 输出格式不符合预期）
/// </summary>
public class FormatErrorException : InterruptAgentFlowException
{
    public FormatErrorException(ChatMessage feedbackMessage) : base(feedbackMessage)
    {
    }
}

/// <summary>
/// 用户中断
/// </summary>
public class UserInterruptionException : InterruptAgentFlowException
{
    public UserInterruptionException(ChatMessage exitMessage) : base(exitMessage)
    {
    }
}