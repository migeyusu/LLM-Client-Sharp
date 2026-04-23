using System.ClientModel;
using System.ClientModel.Primitives;
using System.Diagnostics;
using System.Text;
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
    private readonly bool _treatNullChoicesAsEmptyResponse;

    public OpenAIChatClientEx(string model, ApiKeyCredential credential, OpenAIClientOptions options,
        bool treatNullChoicesAsEmptyResponse = false)
        : base(model, credential, options)
    {
        _treatNullChoicesAsEmptyResponse = treatNullChoicesAsEmptyResponse;
    }

    public override async Task<ClientResult> CompleteChatAsync(BinaryContent content, RequestOptions? options = null)
    {
        var clientContext = AsyncContextStore<ChatStackContext>.Current;
        var shouldProcessNonStreamingResponse = clientContext?.Streaming != true;
        var history = clientContext?.CurrentStep?.ProtocolLog;
        if (clientContext != null)
        {
            if (clientContext.AdditionalObjects.Count != 0 || clientContext.ShowRequestJson ||
                clientContext.EnableSchemaCleaning)
            {
                await using (var oriStream = new MemoryStream())
                {
                    await content.WriteToAsync(oriStream);
                    oriStream.Position = 0;
                    var requestObj = await JsonNode.ParseAsync(oriStream);
                    if (requestObj == null)
                    {
                        throw new InvalidOperationException("Content is not valid JSON.");
                    }
                    
                    // 1. 在原有的附加对象逻辑之前或之后，执行 Schema 清洗
                    if (clientContext.EnableSchemaCleaning)
                    {
                        CleanTypeArrays(requestObj);
                    }

                    foreach (var additionalObject in clientContext.AdditionalObjects)
                    {
                        var node = JsonSerializer.SerializeToNode(additionalObject.Value,
                            Extension.DefaultJsonSerializerOptions);
                        requestObj[additionalObject.Key] = node;
                    }
                    
                    // 2. 将清洗/修改后的 JSON 内容进行打印（如果有需要的话）
                    if (clientContext.ShowRequestJson || Debugger.IsAttached)
                    {
                        var jsonOptions = new JsonSerializerOptions
                        {
                            WriteIndented = true,
                            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                        };

                        var formattedJson = requestObj.ToJsonString(jsonOptions);
                        history!.AppendLine("<request>");
                        history.AppendLine(formattedJson);
                        history.AppendLine("</request>");
                    }

                    oriStream.SetLength(0);
                    await using (var writer = new Utf8JsonWriter(oriStream))
                    {
                        requestObj.WriteTo(writer);
                        await writer.FlushAsync();
                    }

                    oriStream.Position = 0;
                    var modifiedData = await BinaryData.FromStreamAsync(oriStream);
                    content = BinaryContent.Create(modifiedData);
                }
            }
        }

        var result = await base.CompleteChatAsync(content, options);

        if (clientContext != null)
        {
            clientContext.ResponseResult = result;
        }

        if (shouldProcessNonStreamingResponse)
        {
            await NormalizeChatCompletionResponseAsync(result, clientContext);
            await ValidateChatCompletionResponseAsync(result);
        }

#if DEBUG

        if (clientContext != null)
        {
            history!.AppendLine("<response>");
            if (shouldProcessNonStreamingResponse)
            {
                var response = await GetResponseTextAsync(result.GetRawResponse());
                history.AppendLine(response ?? string.Empty);
            }
            else
            {
                history.AppendLine("<streaming response omitted>");
            }

            history.AppendLine("</response>");
        }
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

        return result;
    }

    private async Task NormalizeChatCompletionResponseAsync(ClientResult result, ChatStackContext? clientContext)
    {
        var response = result.GetRawResponse();
        var responseText = await GetResponseTextAsync(response);
        if (string.IsNullOrWhiteSpace(responseText))
        {
            return;
        }

        try
        {
            using var document = JsonDocument.Parse(responseText);
            var root = document.RootElement;
            if (!_treatNullChoicesAsEmptyResponse || root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("choices", out var choicesElement) ||
                choicesElement.ValueKind != JsonValueKind.Null)
            {
                return;
            }

            var rootNode = JsonNode.Parse(responseText)?.AsObject();
            if (rootNode == null)
            {
                return;
            }

            rootNode["choices"] = new JsonArray
            {
                new JsonObject
                {
                    ["index"] = 0,
                    ["message"] = new JsonObject
                    {
                        ["role"] = "assistant",
                        ["content"] = string.Empty
                    },
                    ["finish_reason"] = "stop"
                }
            };
            var normalizedResponseText = rootNode.ToJsonString();
            ReplaceResponseContent(response, normalizedResponseText);
            clientContext?.CurrentStep?.ProtocolLog?.AppendLine(
                "<warning>Applied OpenAI-compatible fallback: converted null choices to an empty array.</warning>");
        }
        catch (JsonException)
        {
            // Let the stricter validation stage surface the invalid JSON error.
        }
    }

    private static async Task ValidateChatCompletionResponseAsync(ClientResult result)
    {
        var response = result.GetRawResponse();
        var responseText = await GetResponseTextAsync(response);
        if (string.IsNullOrWhiteSpace(responseText))
        {
            throw new LlmInvalidRequestException(
                "The LLM endpoint returned an empty OpenAI-compatible response.");
        }

        try
        {
            using var document = JsonDocument.Parse(responseText);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                throw new LlmInvalidRequestException(
                    $"The LLM endpoint returned an invalid OpenAI-compatible response. Expected a JSON object but got {root.ValueKind}.\nResponse Content: {TrimResponse(responseText)}");
            }

            if (!root.TryGetProperty("choices", out var choicesElement))
            {
                throw new LlmInvalidRequestException(
                    $"The LLM endpoint returned an invalid OpenAI-compatible response. Missing required 'choices' field.\nResponse Content: {TrimResponse(responseText)}");
            }

            if (choicesElement.ValueKind != JsonValueKind.Array)
            {
                throw new LlmInvalidRequestException(
                    $"The LLM endpoint returned an invalid OpenAI-compatible response. Expected 'choices' to be an array, but got {choicesElement.ValueKind}.\nResponse Content: {TrimResponse(responseText)}");
            }
        }
        catch (JsonException ex)
        {
            throw new LlmInvalidRequestException(
                $"The LLM endpoint returned invalid JSON for an OpenAI-compatible response.\nResponse Content: {TrimResponse(responseText)}",
                ex);
        }
    }


    private static async Task<string?> GetResponseTextAsync(PipelineResponse response)
    {
        var contentStream = response.ContentStream;
        if (contentStream == null)
        {
            return response.Content.ToString();
        }

        await using var memoryStream = new MemoryStream();
        if (contentStream.CanSeek)
        {
            contentStream.Position = 0;
        }

        await contentStream.CopyToAsync(memoryStream);
        var responseBytes = memoryStream.ToArray();
        ReplaceResponseContent(response, responseBytes);
        return Encoding.UTF8.GetString(responseBytes);
    }

    private static void ReplaceResponseContent(PipelineResponse response, string responseText)
    {
        ReplaceResponseContent(response, Encoding.UTF8.GetBytes(responseText));
    }

    private static void ReplaceResponseContent(PipelineResponse response, byte[] responseBytes)
    {
        response.ContentStream = new MemoryStream(responseBytes, writable: false);
    }

    private static string TrimResponse(string responseText)
    {
        const int maxLength = 2048;
        if (responseText.Length <= maxLength)
        {
            return responseText;
        }

        return responseText[..maxLength] + "...";
    }

    /// <summary>
    /// 递归遍历 JSON AST 节点，将 "type": ["string", "null"] 降维清洗为 "type": "string"
    /// </summary>
    private static void CleanTypeArrays(JsonNode? node)
    {
        if (node is JsonObject jsonObject)
        {
            // 如果发现了 "type" 字段并且它的值是一个数组
            if (jsonObject.TryGetPropertyValue("type", out var typeNode) && typeNode is JsonArray typeArray)
            {
                string mainType = "string"; // Fallback 类型
                foreach (var item in typeArray)
                {
                    var typeValue = item?.GetValue<string>();
                    if (!string.Equals(typeValue, "null", StringComparison.OrdinalIgnoreCase))
                    {
                        // 提取出第一个不是 "null" 的类型（如 "integer", "string"）
                        mainType = typeValue ?? "string";
                        break;
                    }
                }

                // 【核心覆盖】：用单一基础类型覆盖原来的数组
                jsonObject["type"] = mainType;
            }

            // 使用 ToList 生成快照，防范集合在这个遍历过程中被修改引发 InvalidOperationException
            foreach (var kvp in jsonObject.ToList())
            {
                if (kvp.Value != null)
                {
                    CleanTypeArrays(kvp.Value);
                }
            }
        }
        else if (node is JsonArray jsonArray)
        {
            foreach (var item in jsonArray)
            {
                if (item != null)
                {
                    CleanTypeArrays(item);
                }
            }
        }
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