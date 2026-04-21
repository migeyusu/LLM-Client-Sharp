using Microsoft.Extensions.AI;

namespace LLMClient.Abstraction;

public sealed class ReactHistorySegmentation
{
    /// <summary>
    /// 表示跨AgentID的消息
    /// </summary>
    public List<ChatMessage> PreambleMessages { get; } = [];

    public List<ReactHistoryRound> Rounds { get; } = [];
}

