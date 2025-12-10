using System.ClientModel;
using System.ClientModel.Primitives;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using LLMClient.Component.Utility;
using OpenAI;
using OpenAI.Chat;

namespace LLMClient.Endpoints.OpenAIAPI;

/// <summary>
/// 原版的OpenAI ChatClient严格遵守OpenAI的API规范，无法支持许多网站
/// </summary>
public class OpenAIChatClientEx : ChatClient
{
    public OpenAIChatClientEx(string model, ApiKeyCredential credential, OpenAIClientOptions options)
        : base(model, credential, options)
    {
    }

    public override async Task<ClientResult> CompleteChatAsync(BinaryContent content, RequestOptions? options = null)
    {
        var clientContext = AsyncContextStore<ChatContext>.Current;
        if (clientContext != null)
        {
            if (clientContext.AdditionalObjects.Count != 0)
            {
                await using (var oriStream = new MemoryStream())
                {
                    await content
                        .WriteToAsync(oriStream);
                    oriStream.Position = 0;
                    var jsonNode = await JsonNode.ParseAsync(oriStream);
                    if (jsonNode == null)
                    {
                        throw new InvalidOperationException("Content is not valid JSON.");
                    }

                    foreach (var additionalObject in clientContext.AdditionalObjects)
                    {
                        var node = JsonSerializer.SerializeToNode(additionalObject.Value,
                            Extension.DefaultJsonSerializerOptions);
                        jsonNode[additionalObject.Key] = node;
                    }

                    oriStream.SetLength(0);
                    await using (var writer = new Utf8JsonWriter(oriStream))
                    {
                        jsonNode.WriteTo(writer);
                        await writer.FlushAsync();
                    }

                    oriStream.Position = 0;
                    var modifiedData = await BinaryData.FromStreamAsync(oriStream);
                    content = BinaryContent.Create(modifiedData);
                }
            }
        }

        var result = await base.CompleteChatAsync(content, options);
#if DEBUG
        // var binaryData = result.GetRawResponse();
        /*var jsonNode = JsonNode.Parse(contentString);
        if (jsonNode != null)
        {
            var choice = jsonNode["choices"]?.AsArray();

            if (choice != null && choice.Count > 0)
            {
                var message = choice[0]?["message"];
                if (message != null)
                {
                }
            }
        }*/
#endif
        if (clientContext != null)
        {
            clientContext.Result = result;
        }

        return result;
    }


    public override AsyncCollectionResult<StreamingChatCompletionUpdate> CompleteChatStreamingAsync(
        IEnumerable<ChatMessage> messages, ChatCompletionOptions? options = null,
        CancellationToken cancellationToken = new CancellationToken())
    {
        return base.CompleteChatStreamingAsync(messages, options, cancellationToken);
    }
}

/*public class CustomAsyncCollectionResult : AsyncCollectionResult<StreamingChatCompletionUpdate>
{
    private AsyncCollectionResult<StreamingChatCompletionUpdate> _inner;

    public CustomAsyncCollectionResult(AsyncCollectionResult<StreamingChatCompletionUpdate> inner)
    {
        _inner = inner;
    }

    public override IAsyncEnumerable<ClientResult> GetRawPagesAsync()
    {
        return _inner.GetRawPagesAsync();
    }

    public override ContinuationToken? GetContinuationToken(ClientResult page)
    {
        return _inner.GetContinuationToken(page);
    }

    protected override IAsyncEnumerable<StreamingChatCompletionUpdate> GetValuesFromPageAsync(ClientResult page)
    {
        return _inner.GetValuesFromPageAsync(page);
    }
}*/