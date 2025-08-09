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
using LLMClient.Endpoints;
using LLMClient.Endpoints.OpenAIAPI;
using LLMClient.UI;
using LLMClient.UI.Dialog;
using LLMClient.UI.MCP;
using LLMClient.UI.Project;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

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

    public static void UpgradeAPIVersion(this ChatCompletionsClient client, string apiVersion = "2024-12-01-preview")
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
                .ConstructUsing(((po, context) =>
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
                .ConstructUsing((po, context) => new FunctionResultContent(po.CallId, po.Result)
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
            expression.CreateMap<IModelParams, ILLMModel>();
            expression.CreateMap<IModelParams, APIModelInfo>();
            expression.CreateMap<APIEndPoint, APIEndPoint>();
            expression.CreateMap<APIDefaultOption, APIDefaultOption>();
            expression.CreateMap<ILLMClient, LLMClientPersistModel>()
                .ConvertUsing<AutoMapModelTypeConverter>();
            expression.CreateMap<LLMClientPersistModel, ILLMClient>()
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
                    ILLMClient llmClient = NullLlmModelClient.Instance;
                    var client = source.Client;
                    if (client != null)
                    {
                        llmClient = context.Mapper.Map<LLMClientPersistModel, ILLMClient>(client);
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
            : (resultContent.Result?.ToString() ?? "(null)") ?? "");
    }

    public static string GetEnumDescription(this Enum value)
    {
        var type = value.GetType();
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

    public static ILLMClient? CreateClient(this ILLMModel llmModel)
    {
        var endpoint = llmModel.Endpoint;
        return !endpoint.IsEnabled ? null : endpoint?.NewClient(llmModel);
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


    public static ImageSource LoadSvgFromBase64(string src)
    {
        //data:image/svg;base64,
        byte[] binaryData = Convert.FromBase64String(src);
        using (var mem = new MemoryStream(binaryData))
        {
            return mem.ToImageSource(".svg");
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
        T? parent = parentObject as T;
        if (parent != null)
            return parent;
        else
            return FindVisualParent<T>(parentObject);
    }

    public static T Clone<T>(T source) where T : class
    {
        var json = JsonSerializer.Serialize(source);
        return JsonSerializer.Deserialize<T>(json) ?? throw new InvalidOperationException("Deserialization failed.");
    }
}