using LLMClient.Agent;
using LLMClient.Dialog.Models;
using Microsoft.Extensions.AI;

namespace LLMClient.Abstraction;

/// <summary>
/// 对话消息的层次标签系统，提供 5 个级别的消息追踪能力。
/// <para>
/// 层次结构由高到低：
/// <list type="bullet">
///   <item>1. Session level — 同一个 session 的消息具有相同的 session id 标签</item>
///   <item>2. Interaction key — 一次 interaction 包括 request + response，具有相同的 interaction id 标签</item>
///   <item>3. DialogItem level — 一个 dialog item 为 request 或 response 内的所有消息</item>
///   <item>3.5. Agent level — 单个 agent 目前限制在 response 内</item>
///   <item>4. React loop level — 表示一个轮次的请求，包括 assistant + observation，具有相同的 round number 标签</item>
///   <item>5. Message level — 最低级别，单条消息（无专用标签）</item>
/// </list>
/// </para>
/// <para>
/// 标签职责分配：
/// <list type="bullet">
///   <item>Level 1–3 (Session / Interaction / DialogItem): 由 <see cref="DefaultRequestContextBuilder"/> 在 GetMessagesAsync 中打标签</item>
///   <item>Level 3.5 (Agent) + Level 4 (React Loop): 由 LlmClientBase 在 ProduceStepAsync 中打标签</item>
/// </list>
/// </para>
/// </summary>
public static class ChatMessageHierarchy
{
    /// <summary>
    /// react loop level
    /// </summary>
    private const string ReactRoundNumberKey = "llmclient.react.round";

    /// <summary>
    /// react loop level
    /// </summary>
    private const string ReactRoundKindKey = "llmclient.react.kind";

    /// <summary>
    /// agent level
    /// </summary>
    private const string AgentKey = "llmclient.agent";

    /// <summary>
    /// interaction level
    /// </summary>
    private const string InteractionKey = "llmclient.interaction";

    private const string DialogItemKey = "llmclient.dialogitem";

    private const string SessionKey = "llmclient.session";

    public static void TagDialogLevel(this ChatMessage chatMessage, IDialogItem item)
    {
        chatMessage.AdditionalProperties ??= new AdditionalPropertiesDictionary();
        chatMessage.AdditionalProperties[DialogItemKey] = item.Role + "_" + item.Id;
    }

    public static void TagInteractionLevel(this ChatMessage chatMessage, IInteractionItem interaction)
    {
        chatMessage.AdditionalProperties ??= new AdditionalPropertiesDictionary();
        chatMessage.AdditionalProperties[InteractionKey] = interaction.InteractionId;
    }

    public static void TagSessionLevel(this ChatMessage chatMessage, ITextDialogSession session)
    {
        TagSessionLevel(chatMessage, session.ID);
    }

    /// <summary>
    /// 标记消息的 session level，无需完整 session 对象，仅提供 sessionId 即可。
    /// </summary>
    public static void TagSessionLevel(this ChatMessage chatMessage, Guid sessionId)
    {
        chatMessage.AdditionalProperties ??= new AdditionalPropertiesDictionary();
        chatMessage.AdditionalProperties[SessionKey] = sessionId;
    }

    public static void TagAgentLevel(this ChatMessage chatMessage, IAgent agent)
    {
        chatMessage.AdditionalProperties ??= new AdditionalPropertiesDictionary();
        chatMessage.AdditionalProperties[AgentKey] = agent.Name;
    }

    public static void TagLoopLevel(this IEnumerable<ChatMessage> messages, int roundNumber,
        ReactHistoryMessageKind kind)
    {
        foreach (var message in messages)
        {
            message.TagLoopLevel(roundNumber, kind);
        }
    }

    public static void TagLoopLevel(this ChatMessage message, int roundNumber, ReactHistoryMessageKind kind)
    {
        message.AdditionalProperties ??= new AdditionalPropertiesDictionary();
        message.AdditionalProperties[ReactRoundNumberKey] = roundNumber;
        message.AdditionalProperties[ReactRoundKindKey] = kind.ToString();
    }


    public static List<ReactRound> SegmentReactLevel(this ResponseViewItemBase response)
    {
        var chatMessages = response.Messages.ToArray();
        if (chatMessages.Any(message => !message.TryGetRoundNumber(out _)))
        {
            //转为legacy模式
            return LegacySegmentByLinearReading(response).Rounds;
        }

        //默认responseitem内的所有内容都属于同一个agent，且按照react loop的方式组织，因此可以直接对responseitem内的消息进行分割
        var rounds = new Dictionary<int, ReactRound>();
        foreach (var message in chatMessages)
        {
            message.TryGetRoundNumber(out var roundNumber);
            if (!rounds.TryGetValue(roundNumber, out var round))
            {
                round = new ReactRound
                {
                    RoundNumber = roundNumber,
                };
                rounds[roundNumber] = round;
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

        return rounds.OrderBy(kv => kv.Key)
            .Select(pair => pair.Value).ToList();
    }

    public static SegmentedHistory SegmentReactLevel(this IReadOnlyList<ChatMessage> chatHistory,
        string dialogIdFilter)
    {
        var rounds = new Dictionary<int, ReactRound>();
        var roundOrder = new List<int>();
        var chatMessages = new List<ChatMessage>();
        foreach (var message in chatHistory)
        {
            if (TryGetDialogId(message) != dialogIdFilter)
            {
                chatMessages.Add(message);
                continue;
            }

            if (!TryGetRoundNumber(message, out var roundNumber))
            {
                chatMessages.Add(message);
                continue;
            }

            if (!rounds.TryGetValue(roundNumber, out var round))
            {
                round = new ReactRound
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

        var segmentation = new SegmentedHistory()
        {
            PreambleMessages = chatMessages
        };
        foreach (var roundNumber in roundOrder.OrderBy(number => number))
        {
            segmentation.Rounds.Add(rounds[roundNumber]);
        }

        return segmentation;
    }

    /// <summary>
    /// 通过线性读取方式对历史消息进行 ReAct Loop 分割。
    /// 不依赖 AdditionalProperties 中的标签，而是根据消息内容判断：
    /// role 为 Assistant 且包含 FunctionCall 的消息，
    /// 与随后出现的 role 为 Tool 且包含对应 FunctionCall 结果的消息，
    /// 组成一个 ReAct Loop。
    /// </summary>
    public static SegmentedHistory LegacySegmentByLinearReading(IResponse dialogItem)
    {
        var chatHistory = dialogItem.Messages.ToArray();
        var segmentation = new SegmentedHistory();
        if (chatHistory.Length < 2)
        {
            return segmentation;
        }

        var roundNumber = 1;
        for (var i = 0; i < chatHistory.Length; i++)
        {
            var message = chatHistory[i];

            if (message.Role == ChatRole.Assistant)
            {
                if (i + 1 >= chatHistory.Length)
                {
                    break;
                }

                var nextMessage = chatHistory[i + 1];
                if (nextMessage.Role == ChatRole.Tool)
                {
                    segmentation.Rounds.Add(new ReactRound()
                    {
                        RoundNumber = roundNumber,
                        AssistantMessage = message,
                        ObservationMessage = nextMessage
                    });
                    roundNumber++;
                    i++; // skip the paired tool message
                }
                else
                {
                    break;
                }
            }
        }

        return segmentation;
    }

    private static bool TryGetRoundNumber(this ChatMessage message, out int roundNumber)
    {
        roundNumber = 0;
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

    private static string? TryGetDialogId(ChatMessage message)
    {
        var additionalProperties = message.AdditionalProperties;
        if (additionalProperties == null ||
            !additionalProperties.TryGetValue(DialogItemKey, out var value))
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