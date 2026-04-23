using System.Diagnostics.CodeAnalysis;
using Elsa.Extensions;
using LLMClient.Endpoints;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace LLMClient.Abstraction;

public static class ChatEndpointExtensions
{
    /// <summary>
    /// 兼容层：将 IAsyncEnumerable&lt;ReactStep&gt; 聚合为单一 ChatCallResult。
    /// 用于所有仍需 ChatCallResult 的消费者。
    /// </summary>
    public static async Task<AgentTaskResult> SendRequestCompatAsync(
        this IChatEndpoint endpoint,
        RequestContext context,
        CancellationToken cancellationToken = default)
    {
        var totalResult = new AgentTaskResult();
        await foreach (var step in endpoint.SendRequestAsync(context, cancellationToken))
        {
            // 必须消费完内层事件流，否则 step.Result 不可用
            await foreach (var _ in step.WithCancellation(cancellationToken))
            {
            }

            var result = step.Result;
            if (result == null) continue;
            totalResult.Add(result);
        }

        return totalResult;
    }

    public static IChatClient UseContextProvider(this IChatClient client,
        IEnumerable<AIContextProvider> contextProviders)
    {
        var chatClientAgent = client.AsAIAgent(new ChatClientAgentOptions()
        {
            UseProvidedChatClientAsIs = true,
            AIContextProviders = contextProviders
        });
        return new AgentFallbackClient(chatClientAgent);
    }

    public static ChatMessage ToSingle(this IList<ChatMessage> chatMessages)
    {
        if (chatMessages.Count == 0)
        {
            throw new NotSupportedException("空消息列表!");
        }

        var single = chatMessages[0];
        if (chatMessages.Count == 1)
        {
            return single;
        }

        for (var i = 1; i < chatMessages.Count; i++)
        {
            single.Contents.AddRange(chatMessages[i].Contents);
        }

        return single;
    }
}