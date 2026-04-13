using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.Extensions.AI;

namespace LLMClient.Abstraction;

public static class CoreExtension
{
    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "get_DebuggerDisplay")]
    public static extern string GetDebuggerDisplay(FunctionCallContent content);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "get_DebuggerDisplay")]
    public static extern string GetDebuggerDisplay(FunctionResultContent content);

    private const string TokensCounterKey = "TokensCounter";

    internal static void ClearTokensCounterTag(this IEnumerable<ChatMessage> messages)
    {
        foreach (var message in messages)
        {
            message.AdditionalProperties?.Remove(TokensCounterKey);
        }
    }

    internal static long? GetToken(this ChatMessage message)
    {
        if (message.AdditionalProperties?.TryGetValue(TokensCounterKey, out var tokensCounterValue) == true &&
            long.TryParse(tokensCounterValue?.ToString(), out var tokens))
        {
            return tokens;
        }

        return null;
    }

    /// <summary>
    /// 为单个 message 打上 token 标记
    /// </summary>
    internal static void TagTokensCounter(this ChatMessage message, long tokens)
    {
        message.AdditionalProperties ??= new AdditionalPropertiesDictionary();
        message.AdditionalProperties[TokensCounterKey] = tokens;
    }

    /// <summary>
    /// 除了最后一个message外全部设为0
    /// </summary>
    /// <param name="messages"></param>
    /// <param name="completionToken"></param>
    internal static void TagTokensCounter(this IList<ChatMessage> messages, long completionToken)
    {
        for (var i = 0; i < messages.Count; i++)
        {
            var chatMessage = messages[i];
            chatMessage.AdditionalProperties = new AdditionalPropertiesDictionary();
            if (i == messages.Count - 1)
            {
                chatMessage.AdditionalProperties[TokensCounterKey] = completionToken;
            }
            else
            {
                chatMessage.AdditionalProperties[TokensCounterKey] = 0;
            }
        }
    }

    internal static async Task<long> EstimateTokens(this ITokensCounter tokensCounter,
        IReadOnlyList<ChatMessage> messages)
    {
        long totalTokens = 0;
        var contentBuilder = new StringBuilder();
        foreach (var message in messages)
        {
            long? tokens = 0;
            if ((tokens = message.GetToken()) != null)
            {
                totalTokens += tokens.Value;
                continue;
            }

            foreach (var content in message.Contents)
            {
                switch (content)
                {
                    case TextContent textContent:
                        contentBuilder.Append(textContent.Text);
                        break;
                    case TextReasoningContent reasoningContent:
                        contentBuilder.Append(reasoningContent.Text);
                        break;
                    case FunctionCallContent functionCallContent:
                        contentBuilder.Append(GetDebuggerDisplay(functionCallContent));
                        break;
                    case FunctionResultContent functionResultContent:
                        contentBuilder.Append(GetDebuggerDisplay(functionResultContent));
                        break;
                }
            }

            var countTokens = await tokensCounter.CountTokens(contentBuilder.ToString());
            message.AdditionalProperties ??= new AdditionalPropertiesDictionary();
            message.AdditionalProperties[TokensCounterKey] = countTokens;
            totalTokens += countTokens;
        }

        return totalTokens;
    }
}