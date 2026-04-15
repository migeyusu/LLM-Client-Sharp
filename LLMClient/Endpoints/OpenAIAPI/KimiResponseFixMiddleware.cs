using System.Runtime.CompilerServices;
using System.Text.Json;
using Betalgo.Ranul.OpenAI.Contracts.Enums;
using Betalgo.Ranul.OpenAI.ObjectModels.ResponseModels;
using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel;

namespace LLMClient.Endpoints.OpenAIAPI;

public class ResponseFixMiddleware : DelegatingChatClient
{
    public ResponseFixMiddleware(IChatClient innerClient) : base(innerClient)
    {
    }

    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var response = await base.GetResponseAsync(messages, options, cancellationToken);
        return response;
    }

    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Streaming 模式下逐 chunk 修复（Kimi streaming 也会返回 reasoning_content）
        await foreach (var update in base.GetStreamingResponseAsync(messages, options, cancellationToken))
        {
            var chatMessage = ((ChatCompletionCreateResponse?)update.RawRepresentation)?.Choices?.LastOrDefault()
                ?.Delta;
            if (chatMessage != null)
            {
                MapReasoningContent(chatMessage, update.Contents);
            }

            yield return update;
        }
    }


    private static void MapReasoningContent(Betalgo.Ranul.OpenAI.ObjectModels.RequestModels.ChatMessage response,
        IList<AIContent> contents)
    {
        if (response.Role == ChatCompletionRole.Assistant)
        {
            if (!string.IsNullOrEmpty(response.ReasoningContent))
            {
                contents.Add(new TextReasoningContent(response.ReasoningContent));
            }
        }
    }
}