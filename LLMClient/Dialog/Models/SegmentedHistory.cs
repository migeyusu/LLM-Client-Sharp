using Microsoft.Extensions.AI;

namespace LLMClient.Dialog.Models;

/// <summary>
/// 分段的 ReAct 历史记录，包含一个前置消息列表和多个轮次，每个轮次包含一个 assistant 消息和一个 observation 消息。
/// </summary>
public sealed class SegmentedHistory
{
    /// <summary>
    /// 表示跨AgentID的消息
    /// </summary>
    public IReadOnlyList<ChatMessage> PreambleMessages { get; set; } = [];

    /// <summary>
    /// <remarks>對rounds的更改都應該考慮該chatmessage可能會影響主消息的情況，使用副本</remarks>
    /// </summary>
    public List<ReactRound> Rounds { get; } = [];

    public int MaxRoundNumber => Rounds.Count == 0 ? 0 : Rounds.Max(round => round.RoundNumber);

    public IReadOnlyList<ChatMessage> AllMessages
    {
        get
        {
            var all = new List<ChatMessage>(PreambleMessages);
            foreach (var round in Rounds)
            {
                if (round.AssistantMessage != null)
                {
                    all.Add(round.AssistantMessage);
                }

                if (round.ObservationMessage != null)
                {
                    all.Add(round.ObservationMessage);
                }
            }

            return all;
        }
    }
    

    public async Task TryApplyCompressItems(int roundsToKeep, Func<ReactRound, Task<ReactRound>> compressAction)
    {
        if (this.Rounds.Count <= roundsToKeep)
        {
            return;
        }

        var keepFromLengh = this.Rounds.Count - roundsToKeep;
        for (var index = 0; index < keepFromLengh; index++)
        {
            var round = this.Rounds[index];
            if (round.IsCompressApplied)
            {
                continue;
            }

            this.Rounds[index] = await compressAction.Invoke(round);
            round.IsCompressApplied = true;
        }
    }
}