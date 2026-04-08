using Microsoft.Extensions.AI;

namespace LLMClient.Abstraction;

public static class ReactHistorySegmenter
{
    private const string ReactRoundNumberKey = "llmclient.react.round";

    private const string ReactRoundKindKey = "llmclient.react.kind";

    public const int CompressedSummaryRoundNumber = 0;

    public static void TagMessages(IEnumerable<ChatMessage> messages, int roundNumber, ReactHistoryMessageKind kind)
    {
        foreach (var message in messages)
        {
            TagMessage(message, roundNumber, kind);
        }
    }

    public static void TagMessage(ChatMessage message, int roundNumber, ReactHistoryMessageKind kind)
    {
        message.AdditionalProperties ??= new AdditionalPropertiesDictionary();
        message.AdditionalProperties[ReactRoundNumberKey] = roundNumber;
        message.AdditionalProperties[ReactRoundKindKey] = kind.ToString();
    }

    public static ReactHistorySegmentation Segment(IReadOnlyList<ChatMessage> chatHistory)
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
                round.ObservationMessages.Add(message);
            }
            else
            {
                round.AssistantMessages.Add(message);
            }
        }

        foreach (var roundNumber in roundOrder.OrderBy(number => number))
        {
            segmentation.Rounds.Add(rounds[roundNumber]);
        }

        return segmentation;
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

