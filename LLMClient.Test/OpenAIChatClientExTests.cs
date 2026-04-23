using System.ClientModel;
using System.ClientModel.Primitives;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text;
using LLMClient.Component.Utility;
using LLMClient.Endpoints;
using LLMClient.Endpoints.OpenAIAPI;
using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using OpenAI;

namespace LLMClient.Test;

[Obsolete]
[Experimental("SCME0001")]
public class OpenAiChatClientExTests
{
    [Fact]
    public async Task GetResponseAsync_WhenChoicesIsNull_ThrowsLlmInvalidRequestException()
    {
        using var chatClient = CreateChatClient("""
                                                {
                                                  "id": "",
                                                  "object": "",
                                                  "created": 0,
                                                  "model": "ZhipuAI/GLM-5",
                                                  "system_fingerprint": "",
                                                  "choices": null,
                                                  "usage": {
                                                    "prompt_tokens": 0,
                                                    "completion_tokens": 0,
                                                    "total_tokens": 0
                                                  }
                                                }
                                                """);

        var exception = await Assert.ThrowsAsync<LlmInvalidRequestException>(() =>
            chatClient.GetResponseAsync([new ChatMessage(ChatRole.User, "Hello")])) ;

        Assert.Contains("invalid OpenAI-compatible response", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("choices", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ZhipuAI/GLM-5", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetResponseAsync_WhenChoicesIsMissing_ThrowsLlmInvalidRequestException()
    {
        using var chatClient = CreateChatClient("""
                                                {
                                                  "id": "chatcmpl-missing-choices",
                                                  "object": "chat.completion",
                                                  "created": 123,
                                                  "model": "gpt-4o",
                                                  "usage": {
                                                    "prompt_tokens": 1,
                                                    "completion_tokens": 1,
                                                    "total_tokens": 2
                                                  }
                                                }
                                                """);

        var exception = await Assert.ThrowsAsync<LlmInvalidRequestException>(() =>
            chatClient.GetResponseAsync([new ChatMessage(ChatRole.User, "Hello")])) ;

        Assert.Contains("Missing required 'choices' field", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetResponseAsync_WhenChoicesIsValid_ReturnsResponse()
    {
        using var chatClient = CreateChatClient("""
                                                {
                                                  "id": "chatcmpl-valid",
                                                  "object": "chat.completion",
                                                  "created": 123,
                                                  "model": "gpt-4o",
                                                  "choices": [
                                                    {
                                                      "index": 0,
                                                      "message": {
                                                        "role": "assistant",
                                                        "content": "hello world"
                                                      },
                                                      "finish_reason": "stop"
                                                    }
                                                  ],
                                                  "usage": {
                                                    "prompt_tokens": 1,
                                                    "completion_tokens": 2,
                                                    "total_tokens": 3
                                                  }
                                                }
                                                """);

        var response = await chatClient.GetResponseAsync([new ChatMessage(ChatRole.User, "Hello")]);

        Assert.Equal("hello world", response.Text);
    }

    [Fact]
    public async Task GetResponseAsync_WhenChoicesIsNullAndFallbackEnabled_ReturnsEmptyResponse()
    {
        using var chatClient = CreateChatClient("""
                                                {
                                                  "id": "",
                                                  "object": "",
                                                  "created": 0,
                                                  "model": "ZhipuAI/GLM-5",
                                                  "system_fingerprint": "",
                                                  "choices": null,
                                                  "usage": {
                                                    "prompt_tokens": 0,
                                                    "completion_tokens": 0,
                                                    "total_tokens": 0
                                                  }
                                                }
                                                """, true);

        var response = await chatClient.GetResponseAsync([new ChatMessage(ChatRole.User, "Hello")]);

        Assert.Equal(string.Empty, response.Text);
        Assert.NotNull(response.Messages);
        Assert.All(response.Messages, message => Assert.Equal(string.Empty, message.Text));
    }

    [Fact]
    public async Task GetResponseAsync_WhenChoicesIsMissingAndFallbackEnabled_StillThrowsLlmInvalidRequestException()
    {
        using var chatClient = CreateChatClient("""
                                                {
                                                  "id": "chatcmpl-missing-choices",
                                                  "object": "chat.completion",
                                                  "created": 123,
                                                  "model": "gpt-4o",
                                                  "usage": {
                                                    "prompt_tokens": 1,
                                                    "completion_tokens": 1,
                                                    "total_tokens": 2
                                                  }
                                                }
                                                """, true);

        var exception = await Assert.ThrowsAsync<LlmInvalidRequestException>(() =>
            chatClient.GetResponseAsync([new ChatMessage(ChatRole.User, "Hello")]));

        Assert.Contains("Missing required 'choices' field", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CompleteChatAsync_WhenStreamingContextExists_SkipsNonStreamingResponseValidation()
    {
        var responseText = """
                           data: {"id":"chatcmpl-stream","object":"chat.completion.chunk","created":1774824996,"model":"ZhipuAI/GLM-5","choices":[{"index":0,"delta":{"role":"assistant","content":""},"finish_reason":null}],"usage":{"prompt_tokens":0,"completion_tokens":0,"total_tokens":0}}

                           data: [DONE]

                           """;
        var rawClient = CreateRawChatClient(new StreamContent(new MemoryStream(Encoding.UTF8.GetBytes(responseText))));

        System.ClientModel.BinaryContent requestJson =
            System.ClientModel.BinaryContent.Create(BinaryData.FromString("{" +
                "\"messages\":[{\"role\":\"user\",\"content\":\"Hello\"}],\"model\":\"gpt-4o\"}"));

        using var _ = AsyncContextStore<ChatStackContext>.CreateInstance(new ChatStackContext { Streaming = true });
        var result = await rawClient.CompleteChatAsync(requestJson);
        var payload = await ReadRawResponseAsync(result);

        Assert.NotNull(payload);
        Assert.Contains("data:", payload, StringComparison.OrdinalIgnoreCase);
    }

    private static IChatClient CreateChatClient(string responseJson, bool treatNullChoicesAsEmptyResponse = false)
    {
        var openAiClient = CreateOpenAiClient(responseJson, treatNullChoicesAsEmptyResponse);
        var builder = Kernel.CreateBuilder();
        var kernel = builder.AddOpenAIChatCompletion("gpt-4o", openAiClient).Build();
        return kernel.GetRequiredService<IChatCompletionService>().AsChatClient();
    }

    private static OpenAIChatClientEx CreateRawChatClient(string responseJson,
        bool treatNullChoicesAsEmptyResponse = false)
    {
        var openAiClient = CreateOpenAiClient(responseJson, treatNullChoicesAsEmptyResponse);
        return (OpenAIChatClientEx)openAiClient.GetChatClient("gpt-4o");
    }

    private static OpenAIChatClientEx CreateRawChatClient(HttpContent responseContent,
        bool treatNullChoicesAsEmptyResponse = false)
    {
        var openAiClient = CreateOpenAiClient(responseContent, treatNullChoicesAsEmptyResponse);
        return (OpenAIChatClientEx)openAiClient.GetChatClient("gpt-4o");
    }

    private static OpenAIClientEx CreateOpenAiClient(string responseJson, bool treatNullChoicesAsEmptyResponse)
    {
        return CreateOpenAiClient(new StringContent(responseJson, Encoding.UTF8, "application/json"),
            treatNullChoicesAsEmptyResponse);
    }

    private static OpenAIClientEx CreateOpenAiClient(HttpContent responseContent, bool treatNullChoicesAsEmptyResponse)
    {
        var httpMessageHandlerStub = new HttpMessageHandlerStub
        {
            ResponseToReturn = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = responseContent
            }
        };

        var httpClient = new HttpClient(httpMessageHandlerStub);
        var openAiClient = new OpenAIClientEx(new ApiKeyCredential("apiToken"), new OpenAIClientOptions()
        {
            Endpoint = new Uri("https://api.openai.net/v1/"),
            Transport = new HttpClientPipelineTransport(httpClient),
            RetryPolicy = new ClientRetryPolicy(0),
            NetworkTimeout = Timeout.InfiniteTimeSpan
        }, treatNullChoicesAsEmptyResponse);
        return openAiClient;
    }

    private static async Task<string?> ReadRawResponseAsync(ClientResult result)
    {
        var stream = result.GetRawResponse().ContentStream;
        if (stream == null)
        {
            return result.GetRawResponse().Content.ToString();
        }

        if (stream.CanSeek)
        {
            stream.Position = 0;
        }

        using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
        var text = await reader.ReadToEndAsync();
        if (stream.CanSeek)
        {
            stream.Position = 0;
        }

        return text;
    }
}


