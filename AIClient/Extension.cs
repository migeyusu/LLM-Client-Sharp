using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Media;
using Azure.AI.Inference;
using LLMClient.Abstraction;
using LLMClient.Data;
using LLMClient.Dialog;
using LLMClient.Endpoints;
using LLMClient.Endpoints.OpenAIAPI;
using LLMClient.MCP;
using LLMClient.Project;
using LLMClient.Rag;
using LLMClient.Rag.Document;
using LLMClient.UI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LLMClient;

public static class Extension
{
    public static JsonSerializerOptions DefaultJsonSerializerOptions { get; } = new()
    {
        Converters =
        {
            new JsonStringEnumConverter(),
        },
        IgnoreReadOnlyProperties = true,
        WriteIndented = true,
    };

    public static void UpgradeApiVersion(this ChatCompletionsClient client, string apiVersion = "2024-12-01-preview")
    {
        var propertyInfo = client.GetType().GetField("_apiVersion", BindingFlags.Instance | BindingFlags.NonPublic);
        propertyInfo?.SetValue(client, apiVersion);
    }

    public static IServiceCollection AddMap(this IServiceCollection collection)
    {
        return collection.AddAutoMapper((provider, expression) =>
        {
            expression.CreateMap<CheckableFunctionGroupTree, AIFunctionGroupPersistObject>()
                .ConvertUsing<AutoMapModelTypeConverter>();
            expression.CreateMap<AIFunctionGroupPersistObject, CheckableFunctionGroupTree>()
                .ConvertUsing<AutoMapModelTypeConverter>();
            expression.CreateMap<IThinkingConfig, GeekAIThinkingConfig>();
            expression.CreateMap<IThinkingConfig, OpenRouterReasoningConfig>();
            expression.CreateMap<IAIContent, AIContent>().IncludeAllDerived();
            expression.CreateMap<AIContent, IAIContent>().IncludeAllDerived();
            expression.CreateMap<ChatMessage, ChatMessagePO>();
            expression.CreateMap<ChatMessagePO, ChatMessage>();
            expression.CreateMap<TextContent, TextContentPO>();
            expression.CreateMap<TextContentPO, TextContent>();
            expression.CreateMap<FunctionCallContent, FunctionCallContentPO>();
            expression.CreateMap<FunctionCallContentPO, FunctionCallContent>();
            expression.CreateMap<DataContent, DataContentPO>()
                .ForMember(po => po.Data, opt => opt.MapFrom(content => content.Data));
            expression.CreateMap<DataContentPO, DataContent>()
                .ConstructUsing(((po, _) =>
                {
                    if (po.Data != null)
                    {
                        return new DataContent(po.Data, po.MediaType);
                    }

                    if (po.Uri != null)
                    {
                        return new DataContent(po.Uri, po.MediaType);
                    }

                    throw new InvalidOperationException();
                }));
            expression.CreateMap<ErrorContent, ErrorContentPO>();
            expression.CreateMap<ErrorContentPO, ErrorContent>();
            expression.CreateMap<FunctionResultContent, FunctionResultContentPO>();
            expression.CreateMap<FunctionResultContentPO, FunctionResultContent>()
                .ConstructUsing((po, _) => new FunctionResultContent(po.CallId, po.Result)
                    { Exception = po.Exception });
            expression.CreateMap<TextReasoningContent, TextReasoningContentPO>();
            expression.CreateMap<TextReasoningContentPO, TextReasoningContent>();
            expression.CreateMap<UriContent, UriContentPO>();
            expression.CreateMap<UriContentPO, UriContent>()
                .ConstructUsing((po => new UriContent(po.Uri!, po.MediaType!)));
            expression.CreateMap<UsageContent, UsageContentPO>();
            expression.CreateMap<UsageContentPO, UsageContent>();
            expression.CreateMap<RequestViewItem, RequestPersistItem>();
            expression.CreateMap<RequestPersistItem, RequestViewItem>();
            expression.CreateMap<IResponse, ResponseViewItem>();
            expression.CreateMap<IModelParams, IModelParams>();
            expression.CreateMap<IModelParams, DefaultModelParam>();
            expression.CreateMap<IModelParams, ILLMChatModel>();
            expression.CreateMap<IModelParams, APIModelInfo>();
            expression.CreateMap<APIEndPoint, APIEndPoint>();
            expression.CreateMap<APIDefaultOption, APIDefaultOption>();
            expression.CreateMap<ILLMChatClient, LLMClientPersistModel>()
                .ConvertUsing<AutoMapModelTypeConverter>();
            expression.CreateMap<LLMClientPersistModel, ILLMChatClient>()
                .ConvertUsing<AutoMapModelTypeConverter>();
            expression.CreateMap<DialogFilePersistModel, DialogFileViewModel>()
                .ConvertUsing<AutoMapModelTypeConverter>();
            expression.CreateMap<DialogFileViewModel, DialogFilePersistModel>()
                .ConvertUsing<AutoMapModelTypeConverter>();
            expression.CreateMap<DialogViewModel, DialogFilePersistModel>()
                .ConvertUsing<AutoMapModelTypeConverter>();
            expression.CreateMap<DialogFilePersistModel, DialogViewModel>()
                .ConvertUsing<AutoMapModelTypeConverter>();
            expression.CreateMap<ResponseViewItem, ResponsePersistItem>()
                .PreserveReferences();
            expression.CreateMap<ResponsePersistItem, ResponseViewItem>()
                .PreserveReferences()
                .ConstructUsing((source, context) =>
                {
                    ILLMChatClient llmClient = NullLlmModelClient.Instance;
                    var client = source.Client;
                    if (client != null)
                    {
                        llmClient = context.Mapper.Map<LLMClientPersistModel, ILLMChatClient>(client);
                    }

                    return new ResponseViewItem(llmClient);
                });
            expression.CreateMap<MultiResponsePersistItem, MultiResponseViewItem>()
                .ConvertUsing<AutoMapModelTypeConverter>();
            expression.CreateMap<MultiResponseViewItem, MultiResponsePersistItem>()
                .ConvertUsing<AutoMapModelTypeConverter>();
            expression.CreateMap<ProjectViewModel, ProjectPersistModel>()
                .ConvertUsing<AutoMapModelTypeConverter>();
            expression.CreateMap<ProjectPersistModel, ProjectViewModel>()
                .ConvertUsing<AutoMapModelTypeConverter>();
            expression.CreateMap<ProjectTaskViewModel, ProjectTaskPersistModel>()
                .ConvertUsing<AutoMapModelTypeConverter>();
            expression.CreateMap<ProjectTaskPersistModel, ProjectTaskViewModel>()
                .ConvertUsing<AutoMapModelTypeConverter>();
            // expression.CreateMap<AzureOption, GithubCopilotEndPoint>();
            expression.ConstructServicesUsing(provider.GetService);
        }, AppDomain.CurrentDomain.GetAssemblies());
    }

    /// <summary>
    /// cache local file to specific folder.
    /// </summary>
    public static string CacheLocalFile(string filePath, string cacheFolder)
    {
        var fileInfo = new FileInfo(filePath);
        if (!fileInfo.Exists)
        {
            throw new FileNotFoundException("The specified file does not exist.", filePath);
        }

        var extension = Path.GetExtension(filePath);
        //确保目录存在
        Directory.CreateDirectory(cacheFolder);
        var newFileName = Guid.NewGuid() + extension;
        var targetPath = Path.GetFullPath(newFileName, cacheFolder);
        fileInfo.CopyTo(targetPath);
        return targetPath;
    }

    public static string GetDebuggerString(this FunctionCallContent functionCallContent)
    {
        string str = "FunctionCall = ";
        var callId = functionCallContent.CallId;
        if (!string.IsNullOrEmpty(callId))
            str = $"{str}{callId}, ";
        return str + (functionCallContent.Arguments != null
            ? $"{functionCallContent.Name}({string.Join(", ", (IEnumerable<KeyValuePair<string, object>>)functionCallContent.Arguments)})"
            : functionCallContent.Name + "()");
    }

    public static string GetDebuggerString(this FunctionResultContent resultContent)
    {
        var str = $"FunctionResult = {resultContent.CallId}, ";
        var exception = resultContent.Exception;
        return str + (exception != null
            ? $"{exception.GetType().Name}(\"{exception.Message}\")"
            : (resultContent.Result?.ToString() ?? "(null)"));
    }


    public static string GetEnumDescription(this Enum value, Type? type = null)
    {
        type ??= value.GetType();
        var name = Enum.GetName(type, value);
        if (name == null)
            return string.Empty;

        var fieldInfo = type.GetField(name);
        if (fieldInfo == null)
            return string.Empty;

        var attributes = fieldInfo.GetCustomAttributes(typeof(DescriptionAttribute), false);
        if (attributes.Length > 0)
        {
            return ((DescriptionAttribute)attributes[0]).Description;
        }

        return name; // 如果没有描述，返回枚举名称
    }

    /// <summary>
    /// 生成包含Enum所有成员及其描述的格式化字符串
    /// </summary>
    /// <param name="enumType">Enum类型</param>
    /// <param name="format">格式模板，{0}=成员名称，{1}=成员描述</param>
    /// <param name="separator">成员之间的分隔符</param>
    /// <param name="prefix">生成的描述前缀</param>
    /// <returns>格式化的描述字符串</returns>
    public static string GenerateEnumDescription(
        Type enumType,
        string format = "{0} ({1})",
        string separator = ", ",
        string prefix = "Possible values: ")
    {
        if (!enumType.IsEnum)
            return string.Empty;
        var generator = new StringBuilder(prefix);
        var names = Enum.GetNames(enumType);
        for (int i = 0; i < names.Length; i++)
        {
            var name = names[i];
            var memberInfo = enumType.GetMember(name).FirstOrDefault();
            var description = memberInfo?
                .GetCustomAttribute<DescriptionAttribute>()?
                .Description ?? name;

            // 格式化当前成员
            generator.AppendFormat(format, name, description);

            // 添加分隔符（除最后一个外）
            if (i < names.Length - 1)
                generator.Append(separator);
        }

        return generator.ToString();
    }

    #region json

    public static JsonNode GetOrCreate(this JsonNode jsonNode, string key)
    {
        if (jsonNode.AsObject().TryGetPropertyValue(key, out var listNode))
        {
            return listNode!;
        }

        var jsonObject = new JsonObject();
        jsonNode[key] = jsonObject;
        return jsonObject;
    }

    /// <summary>
    /// 如果根节点是对象，返回第一个属性的名称；  
    /// 否则抛出异常（数组、空对象、纯值都会被判定为非法）。
    /// </summary>
    public static string GetRootPropertyName(string json)
    {
        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement root = doc.RootElement;

        // 1. 必须是对象
        if (root.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException("根节点不是对象，可能是数组或纯值。");

        // 2. 必须至少有一个属性
        foreach (JsonProperty prop in root.EnumerateObject())
        {
            return prop.Name; // 取到第一个属性名就结束
        }

        throw new InvalidOperationException("根对象为空（没有任何属性）。");
    }

    public static string? FormatJson(string? json)
    {
        try
        {
            if (string.IsNullOrEmpty(json))
            {
                return null;
            }

            if (!json.StartsWith("{") || !json.StartsWith("[") || !json.EndsWith("}") || !json.EndsWith("]"))
            {
                // 如果不是合法的JSON对象或数组，直接返回原始字符串
                return json;
            }

            var utf8Bytes = Encoding.UTF8.GetBytes(json);
            var reader = new Utf8JsonReader(utf8Bytes);
            if (JsonDocument.TryParseValue(ref reader, out JsonDocument? doc))
            {
                using (doc)
                {
                    return JsonSerializer.Serialize(doc, DefaultJsonSerializerOptions);
                }
            }

            return json;
        }
        catch (Exception)
        {
            return null;
        }
    }

    //json schema create code
    /* JsonSerializerOptions options = new()
        {
            TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
            Converters = { new JsonStringEnumConverter() },
        };
        options.MakeReadOnly();
        AIJsonSchemaCreateOptions s_jsonSchemaCreateOptions = new()
        {
            TransformOptions = new()
            {
                DisallowAdditionalProperties = true,
                RequireAllProperties = true,
                MoveDefaultKeywordToDescription = true,
            }
        };
        var jsonElement = AIJsonUtilities.CreateJsonSchema(typeof(SseServerItem), "test description",
            serializerOptions: options, inferenceOptions: s_jsonSchemaCreateOptions);
        var rawText = jsonElement.GetRawText();*/

    #endregion

    public static ILLMChatClient? CreateChatClient(this ILLMChatModel llmModel)
    {
        var endpoint = llmModel.Endpoint;
        return !endpoint.IsEnabled ? null : endpoint.NewChatClient(llmModel);
    }

    public static void AddLine(this IList<string> list, string? msg = null)
    {
        if (!string.IsNullOrEmpty(msg))
        {
            list.Add(msg);
        }

        list.Add(Environment.NewLine);
    }

    public static void NewLine(this IList<string> list, string? msg = null)
    {
        list.Add(Environment.NewLine);
        if (!string.IsNullOrEmpty(msg))
        {
            list.Add(msg);
        }
    }

    // 递归查找子控件
    public static T? FindVisualChild<T>(this DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(parent, i);
            if (child is T t)
                return t;

            var childOfChild = FindVisualChild<T>(child);
            if (childOfChild != null)
                return childOfChild;
        }

        return null;
    }

    public static T? FindVisualParent<T>(this DependencyObject child) where T : DependencyObject
    {
        //get parent item
        DependencyObject? parentObject = VisualTreeHelper.GetParent(child);

        //we've reached the end of the tree
        if (parentObject == null) return null;

        //check if the parent matches the type we're looking for
        if (parentObject is T parent)
            return parent;
        return FindVisualParent<T>(parentObject);
    }

    public static T Clone<T>(T source) where T : class
    {
        var json = JsonSerializer.Serialize(source);
        return JsonSerializer.Deserialize<T>(json) ?? throw new InvalidOperationException("Deserialization failed.");
    }

    public static string GetStructure(this IEnumerable<ChunkNode> nodes)
    {
        var stringBuilder = new StringBuilder();
        foreach (var node in nodes)
        {
            stringBuilder.Append(node.GetStructure());
        }

        return stringBuilder.ToString();
    }

    public static string GetView(this IList<ChunkNode> nodes)
    {
        var stringBuilder = new StringBuilder();
        foreach (var node in nodes)
        {
            stringBuilder.Append(node.GetView());
        }

        return stringBuilder.ToString();
    }

    public static void TryAddAdditionalFunctionCallResult(this IEnumerable<ChunkNode> nodes)
    {
        var chatContext = AsyncContext<ChatContext>.Current;
        if (chatContext == null)
        {
            return;
        }

        var functionCallResult = chatContext.AdditionalFunctionCallResult;
        var promptBuilder = new StringBuilder("Additional images in function results:\n");
        foreach (var chunkNode in nodes)
        {
            RecursiveAdditionalFunctionCallResult(chunkNode, functionCallResult, promptBuilder);
        }

        if (functionCallResult.Count > 0)
        {
            chatContext.AdditionalUserMessage.Append(promptBuilder);
        }
    }

    private static void RecursiveAdditionalFunctionCallResult(this ChunkNode node, List<AIContent> contents,
        StringBuilder stringBuilder)
    {
        var chunk = node.Chunk;
        if (chunk.Type == (int)ChunkType.Page)
        {
            if (chunk.AttachmentContents.Any())
            {
                var title = node.Parent?.Chunk.Title;
                stringBuilder.AppendLine($"Section {title} has {chunk.AttachmentContents.Count} additional images, see attachment.");
                contents.AddRange(chunk.AttachmentContents);
            }
        }
        else
        {
            foreach (var child in node.Children)
            {
                RecursiveAdditionalFunctionCallResult(child, contents, stringBuilder);
            }
        }
    }

    public static IEnumerable<ChunkNode> OrderNode(this IList<ChunkNode> nodes)
    {
        foreach (var chunkNode in nodes)
        {
            var children = chunkNode.Children;
            chunkNode.Children = OrderNode(children).ToList();
        }

        return nodes.OrderBy(node => node.Chunk.Index);
    }

    public static SemanticKernelStore GetStore(this RagOption ragOption)
    {
        ragOption.ThrowIfNotValid();
        var dbConnection = ragOption.DBConnection;
        var embeddingEndpoint = ragOption.EmbeddingEndpoint;
        if (embeddingEndpoint == null)
        {
            throw new InvalidOperationException("Embedding endpoint is not set.");
        }
#pragma warning disable SKEXP0010
        return new SemanticKernelStore(embeddingEndpoint,
            ragOption.EmbeddingModelId ?? "text-embedding-v3", dbConnection);
#pragma warning restore SKEXP0010
    }

    public static IEnumerable<SelectableViewModel<T>> ToSelectable<T>(this IEnumerable<T> collection)
    {
        foreach (var item in collection)
        {
            yield return new SelectableViewModel<T>(item);
        }
    }

    public const int SummarySize = 1024; // 摘要长度

    private const int SummaryTrigger = 3072; // 摘要触发长度

    /// <summary>
    /// 
    /// </summary>
    /// <param name="client"></param>
    /// <param name="clientSemaphore"></param>
    /// <param name="language">0: English, 1: Chinese</param>
    /// <param name="cache"></param>
    /// <param name="logger"></param>
    /// <param name="summarySize"></param>
    /// <param name="retryCount"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public static Func<string, CancellationToken, Task<string>> CreateSummaryDelegate(this ILLMChatClient client,
        SemaphoreSlim clientSemaphore, int language, PromptsCache cache, ILogger? logger = null,
        int summarySize = SummarySize,
        int retryCount = 3)
    {
        var modelParams = client.Parameters;
        modelParams.Streaming = false;
        if (client.Model.MaxTokensEnable)
        {
            modelParams.MaxTokens =
                int.Min(summarySize * 6, client.Model.MaxTokenLimit); // 设置最大令牌数至少为摘要长度的6倍（以包括reasoning）
        }

        return async (content, token) =>
        {
            if (string.IsNullOrEmpty(content))
            {
                logger?.LogInformation("内容为空，不进行摘要。");
                return string.Empty;
            }

            if (content.Length < SummaryTrigger)
            {
                logger?.LogInformation("内容长度未超过{SummaryTrigger}，不进行摘要。", SummaryTrigger);
                return content;
            }

            if (cache.TryGetValue(content, out var result))
            {
                return result;
            }

            var response = new CompletedResult();
            await clientSemaphore.WaitAsync(token);
            try
            {
                //经过测试，使用 ‘Use the same language of the text blocks’ 时，gpt5 nano依然会错乱，对英语文档生成日语总结，所以手动限制语言
                var stringBuilder = new StringBuilder();
                if (language == 0) // 英语
                {
                    stringBuilder.Append(
                        $"Provide a concise and complete summarization of the following text blocks that does not exceed {summarySize} words. " +
                        "\nThis summary must always:" +
                        "\n- Use English" +
                        "\n- Focus on the most significant aspects of the text blocks" +
                        "\n- Include details from any existing summary" +
                        "\nThis summary must never:" +
                        "\n- Critique, correct, interpret, presume, or assume" +
                        "\n- Identify faults, mistakes, misunderstanding, or correctness" +
                        "\n- Analyze what has not occurred" +
                        "\n- Exclude details from any existing summary" +
                        "\n\nPlease summarize the following text blocks until end:\n\n");
                }
                else // 中文
                {
                    stringBuilder.Append(
                        $"请对以下文本块进行简洁而完整的总结，不超过 {summarySize} 字。" +
                        "\n该摘要必须始终：" +
                        "\n- 使用中文" +
                        "\n- 关注文本块最重要的方面" +
                        "\n- 包含任何现有摘要中的详细信息" +
                        "\n该摘要绝不能：" +
                        "\n- 批评、纠正、解释、推测或假设" +
                        "\n- 指出错误、失误、误解或正确性" +
                        "\n- 分析未发生的事情" +
                        "\n- 排除任何现有摘要中的详细信息" +
                        "\n\n请总结以下文本块直到结束：\n\n");
                }

                stringBuilder.Append(content);
                var dialogContext = new DialogContext([
                    new RequestViewItem() { TextMessage = stringBuilder.ToString(), }
                ]);
                int tryCount = 0;
                while (tryCount < retryCount)
                {
                    response = await client.SendRequest(dialogContext, token);
                    tryCount++;
                    var textResponse = response.TextResponse;
                    if (!string.IsNullOrEmpty(textResponse) && !response.IsInterrupt)
                    {
                        cache.TryAdd(content, textResponse);
                        return textResponse;
                    }
                }
            }
            catch (Exception ex)
            {
                logger?.LogError("Summary error: {ErrorMessage}", ex.Message);
            }
            finally
            {
                clientSemaphore.Release();
            }

            throw new InvalidOperationException("LLM response failed after " + retryCount + " attempts. error: " +
                                                response.ErrorMessage);
        };
    }

    public static int CountRecursive(this PDFNode node)
    {
        int count = 1; // 计数当前节点
        foreach (var child in node.Children)
        {
            count += child.CountRecursive(); // 递归计数子节点
        }

        return count;
    }

    /// <summary>
    /// use temp path in current directory. so it can be deleted when exited.
    /// </summary>
    public static string TempPath => Path.GetFullPath("Temp");

    public static string GetTempFilePath(string prefix = "")
    {
        return Path.GetFullPath(prefix + Guid.NewGuid().ToString().Replace('-', '_'), TempPath);
    }
}