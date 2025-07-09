using System.ComponentModel;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Azure.AI.Inference;
using ImageMagick;
using LLMClient.Abstraction;
using LLMClient.Render;
using Markdig;
using Markdig.Wpf;
using Microsoft.Extensions.AI;

namespace LLMClient;

public static class Extension
{
    public static void UpgradeAPIVersion(this ChatCompletionsClient client, string apiVersion = "2024-12-01-preview")
    {
        var propertyInfo = client.GetType().GetField("_apiVersion", BindingFlags.Instance | BindingFlags.NonPublic);
        propertyInfo?.SetValue(client, apiVersion);
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

            using JsonDocument doc = JsonDocument.Parse(json);
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };
            return JsonSerializer.Serialize(doc, options);
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

    public static ImageSource SVGStreamToImageSource(this Stream stream)
    {
        var magickImage = new MagickImage(stream);
        var bitmapSource = magickImage.ToBitmapSource();
        bitmapSource.Freeze();
        return bitmapSource;
    }

    public static ImageSource LoadSvgFromBase64(string src)
    {
        //data:image/svg;base64,
        byte[] binaryData = Convert.FromBase64String(src);
        using (var mem = new MemoryStream(binaryData))
        {
            return mem.SVGStreamToImageSource();
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
}