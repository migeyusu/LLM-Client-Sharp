using System.ClientModel;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using AutoMapper;
using LLMClient.Abstraction;
using LLMClient.Component.Utility;
using LLMClient.Component.ViewModel;
using LLMClient.Dialog;
using LLMClient.Endpoints;
using LLMClient.Rag;
using LLMClient.Rag.Document;
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
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals
    };

    public static async Task<JsonNode?> ToJsonNode(this ClientResult result)
    {
        var rawResponse = result.GetRawResponse();
        var stream = rawResponse.ContentStream;
        if (stream == null)
        {
            return null;
        }

        if (stream.Length == 0)
        {
            return null;
        }

        stream.Position = 0;

        return await JsonNode.ParseAsync(stream);
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

    public static string GetText(this ChatResponseUpdate update)
    {
        var stringBuilder = new StringBuilder();
        foreach (var content in update.Contents)
        {
            if (content is TextContent textContent)
            {
                stringBuilder.Append(textContent.Text);
            }
            else if (content is TextReasoningContent reasoningContent)
            {
                stringBuilder.Append(reasoningContent.Text);
            }
        }

        return stringBuilder.ToString();
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

    public static IServiceCollection AddMap(this IServiceCollection collection)
    {
        return collection.AddAutoMapper((provider, expression) =>
            {
                var dialogMappingProfile = provider.GetService<DialogMappingProfile>();
                // 可以在这里添加全局配置
                expression.AllowNullCollections = true;
                // cfg.Advanced.AllowAdditiveTypeMapCreation = true; // 如有需要\
                // var viewModelFactory = provider.GetRequiredService<IViewModelFactory>();
                expression.AddProfile(dialogMappingProfile);
            },
            Enumerable.Empty<Assembly>());
    }

    public static IReadOnlyList<IDialogItem> PathFromRoot(this IDialogItem item)
    {
        var stack = new Stack<IDialogItem>();
        for (var p = item; p != null; p = p.PreviousItem)
        {
            if (item is RootDialogItem)
            {
                break;
            }

            stack.Push(p);
        }

        return stack.ToList();
    }

    public static bool CanReachRoot(this IDialogItem item)
    {
        for (var p = item; p != null; p = p.PreviousItem)
        {
            if (p is RootDialogItem)
            {
                return true;
            }
        }

        return false;
    }

    public static IDialogItem DefaultLastItem(this IDialogItem item)
    {
        while (true)
        {
            if (item.Children.Any())
            {
                item = item.Children.First();
                continue;
            }

            return item;
        }
    }


    /// <summary>
    /// 删除一次回复
    /// </summary>
    /// <param name="requestItem"></param>
    /// <exception cref="InvalidOperationException"></exception>
    public static void DeleteInteraction(this IRequestItem requestItem)
    {
        var previousItem = requestItem.PreviousItem;
        if (previousItem == null)
        {
            throw new InvalidOperationException("PreviousItem 不能为空");
        }

        previousItem.RemoveChild(requestItem);
        var firstOrDefault = requestItem.Children.FirstOrDefault();
        if (firstOrDefault is IResponseViewItem responseViewItem)
        {
            foreach (var request in responseViewItem.Children)
            {
                previousItem.AppendChild(request);
            }
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="requestItem"></param>
    /// <returns>返回前一个节点</returns>
    /// <exception cref="InvalidOperationException"></exception>
    public static IDialogItem DeleteFrom(this IRequestItem requestItem)
    {
        var previousItem = requestItem.PreviousItem;
        if (previousItem == null)
        {
            throw new InvalidOperationException("PreviousItem 不能为空");
        }

        previousItem.RemoveChild(requestItem);
        return previousItem;
    }

    public static void DeleteAfter(this IResponseViewItem responseViewItem)
    {
        responseViewItem.ClearChildren();
    }

    /// <summary>
    /// 删除单个节点
    /// </summary>
    /// <param name="viewItem"></param>
    /// <returns>后继节点</returns>
    /// <exception cref="InvalidOperationException"></exception>
    public static IDialogItem? Delete(this EraseViewItem viewItem)
    {
        var previousItem = viewItem.PreviousItem;
        if (previousItem == null)
        {
            throw new InvalidOperationException("PreviousItem 不能为空");
        }

        previousItem.RemoveChild(viewItem);
        var sucessor = viewItem.Children.FirstOrDefault();
        if (sucessor != null)
        {
            previousItem.AppendChild(sucessor);
        }

        return sucessor;
    }

    public static IDialogItem InsertBefore(this IRequestItem requestItem, EraseViewItem eraseViewItem)
    {
        var previousItem = requestItem.PreviousItem;
        if (previousItem == null)
        {
            throw new InvalidOperationException("PreviousItem 不能为空");
        }

        previousItem.RemoveChild(requestItem);
        previousItem.AppendChild(eraseViewItem)
            .AppendChild(requestItem);
        return eraseViewItem;
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
    public static string CreateJsonScheme<T>(string? description = null)
    {
        JsonSerializerOptions options = new()
        {
            TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
            Converters = { new JsonStringEnumConverter() },
        };
        options.MakeReadOnly();
        AIJsonSchemaCreateOptions sJsonSchemaCreateOptions = new()
        {
            TransformOptions = new()
            {
                DisallowAdditionalProperties = true,
                RequireAllProperties = true,
                MoveDefaultKeywordToDescription = true,
            }
        };
        var jsonElement = AIJsonUtilities.CreateJsonSchema(typeof(T), description,
            serializerOptions: options, inferenceOptions: sJsonSchemaCreateOptions);
        return jsonElement.GetRawText();
    }

    #endregion

    public static ILLMChatClient? CreateChatClient(this IParameterizedLLMModel parameterizedLlmModel,
        IMapper mapper)
    {
        var client = parameterizedLlmModel.Model.CreateChatClient();
        if (client == null)
        {
            return null;
        }

        mapper.Map(parameterizedLlmModel.Parameters, client.Parameters);
        return client;
    }

    public static ILLMChatClient? CreateChatClient(this IEndpointModel llmModel)
    {
        var endpoint = llmModel.Endpoint;
        if (!endpoint.IsEnabled)
        {
            return null;
        }

        //prevent recursive call
        if (endpoint is EmptyLLMEndpoint or StubEndPoint)
        {
            return null;
        }

        return endpoint.NewChatClient(llmModel);
    }

    public static void AddLine(this IList<string> list, string? msg = null)
    {
        if (!string.IsNullOrEmpty(msg))
        {
            list.Add(msg);
        }

        list.Add(Environment.NewLine);
    }

    public static void NewLine(this ICollection<string> list, string? msg = null)
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
        var json = JsonSerializer.Serialize(source, DefaultJsonSerializerOptions);
        return JsonSerializer.Deserialize<T>(json, DefaultJsonSerializerOptions) ??
               throw new InvalidOperationException("Deserialization failed.");
    }

    public static string HierarchicalMessage(this Exception exception)
    {
        var stringBuilder = new StringBuilder();
        var currentException = exception;
        int level = 0;
        while (currentException != null)
        {
            string indent = new string(' ', level * 2);
            stringBuilder.AppendLine($"{indent}{currentException.Message}");
            currentException = currentException.InnerException;
            level++;
        }

        return stringBuilder.ToString();
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
        var chatContext = AsyncContextStore<ChatContext>.Current;
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
        if (chunk.Type == (int)ChunkType.ContentUnit)
        {
            if (chunk.AttachmentContents.Any())
            {
                var title = node.Parent?.Chunk.Title;
                stringBuilder.AppendLine(
                    $"Section {title} has {chunk.AttachmentContents.Count} additional images, see attachment.");
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

    public static int CountRecursive<T, TK>(this RawNode<T, TK> node) where T : RawNode<T, TK> where TK : IContentUnit
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

    public const string CacheFolderName = "Cache";

    public static string GetTempFilePath(string prefix = "")
    {
        return Path.GetFullPath(prefix + Guid.NewGuid().ToString().Replace('-', '_'), TempPath);
    }

    /// <summary>
    /// 返回一个可等待的对象，用于将执行上下文切换到此Dispatcher关联的线程。
    /// 用法: await myDispatcher.SwitchToAsync();
    /// </summary>
    public static UIThreadAwaitable SwitchToAsync(this Dispatcher dispatcher)
    {
        return new UIThreadAwaitable(dispatcher);
    }

    /// <summary>
    /// recursively copy directory from source to destination
    /// </summary>
    /// <param name="sourceDir"></param>
    /// <param name="destinationDir"></param>
    /// <returns></returns>
    public static async Task CopyDirectoryAsync(string sourceDir, string destinationDir)
    {
        if (string.IsNullOrWhiteSpace(sourceDir))
            throw new ArgumentException("Source directory is null or empty.", nameof(sourceDir));
        if (string.IsNullOrWhiteSpace(destinationDir))
            throw new ArgumentException("Destination directory is null or empty.", nameof(destinationDir));

        var srcInfo = new DirectoryInfo(sourceDir);
        if (!srcInfo.Exists)
            throw new DirectoryNotFoundException($"Source directory not found: {sourceDir}");

        Directory.CreateDirectory(destinationDir);

        // 复制文件（当前目录）
        foreach (var filePath in Directory.EnumerateFiles(sourceDir))
        {
            var destFilePath = Path.Combine(destinationDir, Path.GetFileName(filePath)!);
            using var sourceStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920,
                useAsync: true);
            using var destStream = new FileStream(destFilePath, FileMode.Create, FileAccess.Write, FileShare.None,
                81920, useAsync: true);
            await sourceStream.CopyToAsync(destStream).ConfigureAwait(false);
        }

        // 递归复制子目录
        foreach (var dirPath in Directory.EnumerateDirectories(sourceDir))
        {
            var destSubDir = Path.Combine(destinationDir, Path.GetFileName(dirPath)!);
            await CopyDirectoryAsync(dirPath, destSubDir).ConfigureAwait(false);
        }
    }

    public static float CalculateTps(this IResponse response)
    {
        var duration = response.Duration - response.Latency / 1000.0;
        if (duration <= 0 || response.Tokens == 0)
        {
            return float.NaN;
        }

        return (float)(response.Tokens / duration);
    }
}

/// <summary>
/// 提供线程安全、原子性的 JSON 文件保存扩展方法。
/// </summary>
public static class JsonFileHelper
{
    // 使用 ConcurrentDictionary 存储每个文件路径对应的 SemaphoreSlim
    // 这样可以确保：不同文件的保存操作互不阻塞，但同一文件的保存操作会串行化
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> FileLocks = new();

    /// <summary>
    /// 将对象异步保存为 JSON 文件。支持 JsonNode 和普通类实例。
    /// 包含原子性写入机制（先写临时文件，成功后替换原文件）。
    /// </summary>
    /// <param name="data">要保存的数据对象</param>
    /// <param name="filePath">目标文件路径</param>
    /// <param name="options">JSON 序列化选项（可选）</param>
    public static async Task SaveJsonToFileAsync<T>(this T data, string filePath,
        JsonSerializerOptions? options = null)
    {
        // 获取该文件路径专用的锁
        var semaphore = FileLocks.GetOrAdd(filePath, _ => new SemaphoreSlim(1, 1));

        await semaphore.WaitAsync();
        try
        {
            await InternalSaveAsync(data, filePath, options);
        }
        finally
        {
            semaphore.Release();
        }
    }

    private static async Task InternalSaveAsync<T>(T data, string filePath, JsonSerializerOptions? options)
    {
        // 1. 准备目录
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // 2. 生成临时文件路径
        // 使用 .tmp 后缀，并在同一目录下
        string tempFilePath = filePath + ".tmp";

        try
        {
            // 3. 写入临时文件
            await using (var fileStream =
                         new FileStream(tempFilePath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                // 特殊处理 JsonNode，因为它有更高效的 WriteTo 方法
                if (data is JsonNode jsonNode)
                {
                    // 如果用户没有传 options，我们需要给 JsonWriter 一个默认配置，或者继承 options 的配置
                    // 这里的 Indented 设置参考了常见的默认配置习惯，也可以强制使用 options
                    var writerOptions = new JsonWriterOptions
                    {
                        Indented = options?.WriteIndented ?? false
                    };

                    await using (var utf8JsonWriter = new Utf8JsonWriter(fileStream, writerOptions))
                    {
                        jsonNode.WriteTo(utf8JsonWriter);
                        await utf8JsonWriter.FlushAsync();
                    }
                }
                else
                {
                    // 普通 POCO 类序列化
                    await JsonSerializer.SerializeAsync(fileStream, data, options);
                }
            }

            // 4. 原子性替换文件
            // 如果这一步成功，原文件被瞬间替换（这在 Windows 上是原子操作）。
            File.Move(tempFilePath, filePath, true);
        }
        catch
        {
            // 发生任何异常，尝试清理可能残留的临时文件
            if (File.Exists(tempFilePath))
            {
                try
                {
                    File.Delete(tempFilePath);
                }
                catch
                {
                    /* 忽略清理时的二次错误 */
                }
            }

            throw; // 重新抛出异常供上层捕获
        }
    }
}