using Microsoft.Extensions.AI;

namespace LLMClient.Agent.MiniSWE;

/// <summary>
/// 用于中断 Agent 流程并添加消息的异常基类
/// </summary>
public class AgentFlowException : Exception
{
    public List<ChatMessage> Messages { get; }

    public AgentFlowException(string message, params ChatMessage[] messages) : base(message)
    {
        Messages = messages.ToList();
    }

    public AgentFlowException(string message, IEnumerable<ChatMessage> messages) : base(message)
    {
        Messages = messages.ToList();
    }
}

/// <summary>
/// 任务已提交完成
/// </summary>
public class SubmittedException : AgentFlowException
{
    public string Submission { get; }

    public SubmittedException(string submission, ChatMessage exitMessage) : base(submission, exitMessage)
    {
        Submission = submission;
    }
}

/// <summary>
/// 格式错误（LLM 输出格式不符合预期）
/// </summary>
public class FormatErrorException : AgentFlowException
{
    public FormatErrorException(ChatMessage feedbackMessage) : base("format error", feedbackMessage)
    {
    }
}

public class StepOverflowException : AgentFlowException
{
    public StepOverflowException() : base("Step limit exceeded")
    {
    }
}