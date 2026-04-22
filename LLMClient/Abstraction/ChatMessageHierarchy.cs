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

    public const int CompressedSummaryRoundNumber = 0;

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

    public static void TagLoopLevel(IEnumerable<ChatMessage> messages, int roundNumber, ReactHistoryMessageKind kind,
        string? agentId = null)
    {
        foreach (var message in messages)
        {
            TagLoopLevel(message, roundNumber, kind, agentId);
        }
    }

    public static void TagLoopLevel(ChatMessage message, int roundNumber, ReactHistoryMessageKind kind,
        string? agentId = null)
    {
        message.AdditionalProperties ??= new AdditionalPropertiesDictionary();
        message.AdditionalProperties[ReactRoundNumberKey] = roundNumber;
        message.AdditionalProperties[ReactRoundKindKey] = kind.ToString();
        if (agentId != null)
        {
            message.AdditionalProperties[AgentKey] = agentId;
        }
    }

    public static ReactHistorySegmentation SegmentReactLevel(IReadOnlyList<ChatMessage> chatHistory, string? agentIdFilter = null)
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

    /// <summary>
    /// 通过线性读取方式对历史消息进行 ReAct Loop 分割。
    /// 不依赖 AdditionalProperties 中的标签，而是根据消息内容判断：
    /// role 为 Assistant 且包含 FunctionCall 的消息，
    /// 与随后出现的 role 为 Tool 且包含对应 FunctionCall 结果的消息，
    /// 组成一个 ReAct Loop。
    /// </summary>
    public static ReactHistorySegmentation SegmentByLinearReading(IDialogItem dialogItem)
    {
        var chatHistory = dialogItem.Messages.ToArray();
        var segmentation = new ReactHistorySegmentation();
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
                    segmentation.Rounds.Add(new ReactHistoryRound()
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
            !additionalProperties.TryGetValue(AgentKey, out var value))
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
