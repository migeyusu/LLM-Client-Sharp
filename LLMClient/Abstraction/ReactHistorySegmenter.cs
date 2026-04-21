using Microsoft.Extensions.AI;

namespace LLMClient.Abstraction;

public static class ReactHistorySegmenter
{
    private const string ReactRoundNumberKey = "llmclient.react.round";

    private const string ReactRoundKindKey = "llmclient.react.kind";

    private const string ReactRoundAgentKey = "llmclient.react.agent";

    public const int CompressedSummaryRoundNumber = 0;

    public static void TagMessages(IEnumerable<ChatMessage> messages, int roundNumber, ReactHistoryMessageKind kind, string? agentId = null)
    {
        foreach (var message in messages)
        {
            TagMessage(message, roundNumber, kind, agentId);
        }
    }

    public static void TagMessage(ChatMessage message, int roundNumber, ReactHistoryMessageKind kind, string? agentId = null)
    {
        message.AdditionalProperties ??= new AdditionalPropertiesDictionary();
        message.AdditionalProperties[ReactRoundNumberKey] = roundNumber;
        message.AdditionalProperties[ReactRoundKindKey] = kind.ToString();
        if (agentId != null)
        {
            message.AdditionalProperties[ReactRoundAgentKey] = agentId;
        }
    }

    public static ReactHistorySegmentation Segment(IReadOnlyList<ChatMessage> chatHistory, string? agentIdFilter = null)
    {
        var segmentation = new ReactHistorySegmentation();
        var rounds = new Dictionary<int, ReactHistoryRound>();
        var roundOrder = new List<int>();

        foreach (var message in chatHistory)
        {
            if (!TryGetRoundNumber(message, out var roundNumber))
            {
                segmentation.PreambleMessages.Add(message);
                continue;
            }

            // 如果指定了 agentIdFilter，且消息的 agentId 不匹配，则视为 Preamble
            if (agentIdFilter != null && TryGetAgentId(message) != agentIdFilter)
            {
                segmentation.PreambleMessages.Add(message);
                continue;
            }

            if (!rounds.TryGetValue(roundNumber, out var round))
            {
                round = new ReactHistoryRound
                {
                    RoundNumber = roundNumber,
                };
                rounds[roundNumber] = round;
                roundOrder.Add(roundNumber);
            }

            var kind = GetMessageKind(message);
            if (kind == ReactHistoryMessageKind.Observation)
            {
                round.ObservationMessage = message;
            }
            else
            {
                round.AssistantMessage = message;
            }
        }

        foreach (var roundNumber in roundOrder.OrderBy(number => number))
        {
            segmentation.Rounds.Add(rounds[roundNumber]);
        }

        return segmentation;
    }

    /// <summary>
    /// 获取指定 agent 在历史中的最大 round number。如果历史中没有该 agent 的消息，返回 0。
    /// </summary>
    public static int GetMaxRoundNumber(IReadOnlyList<ChatMessage> chatHistory, string? agentId = null)
    {
        var maxRound = 0;
        foreach (var message in chatHistory)
        {
            if (!TryGetRoundNumber(message, out var roundNumber))
                continue;

            if (agentId != null && TryGetAgentId(message) != agentId)
                continue;

            if (roundNumber > maxRound)
                maxRound = roundNumber;
        }
        return maxRound;
    }

    private static bool TryGetRoundNumber(ChatMessage message, out int roundNumber)
    {
        roundNumber = default;
        var additionalProperties = message.AdditionalProperties;
        if (additionalProperties == null ||
            !additionalProperties.TryGetValue(ReactRoundNumberKey, out var value) ||
            value == null)
        {
            return false;
        }

        switch (value)
        {
            case int intValue:
                roundNumber = intValue;
                return true;
            case long longValue when longValue >= int.MinValue && longValue <= int.MaxValue:
                roundNumber = (int)longValue;
                return true;
            case string stringValue when int.TryParse(stringValue, out var parsed):
                roundNumber = parsed;
                return true;
            default:
                return false;
        }
    }

    private static string? TryGetAgentId(ChatMessage message)
    {
        var additionalProperties = message.AdditionalProperties;
        if (additionalProperties == null ||
            !additionalProperties.TryGetValue(ReactRoundAgentKey, out var value))
        {
            return null;
        }
        return value?.ToString();
    }

    private static ReactHistoryMessageKind GetMessageKind(ChatMessage message)
    {
        var additionalProperties = message.AdditionalProperties;
        if (additionalProperties != null &&
            additionalProperties.TryGetValue(ReactRoundKindKey, out var value) &&
            value is string stringValue &&
            Enum.TryParse<ReactHistoryMessageKind>(stringValue, true, out var kind))
        {
            return kind;
        }

        return message.Role == ChatRole.Tool
            ? ReactHistoryMessageKind.Observation
            : ReactHistoryMessageKind.Assistant;
    }
}

