using System.Text.Json;
using System.Text.Json.Serialization;
using LLMClient.Endpoints;
using LLMClient.Endpoints.OpenAIAPI;
using LLMClient.ToolCall.Servers;
using Microsoft.Extensions.AI;
using OpenAI.Chat;

namespace LLMClient.Data;

[JsonSourceGenerationOptions(WriteIndented = true, GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(DialogFilePersistModel))]
[JsonSerializable(typeof(ProjectPersistModel))]
[JsonSerializable(typeof(CSharpProjectPersistModel))]
[JsonSerializable(typeof(CppProjectPersistModel))]
[JsonSerializable(typeof(DateTimeOffset))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(object[]))]
[JsonSerializable(typeof(string[]))]
[JsonSerializable(typeof(Dictionary<string, object>))]
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSerializable(typeof(List<object>))]
[JsonSerializable(typeof(List<string>))]
[JsonSerializable(typeof(object))]
[JsonSerializable(typeof(ChatTokenUsage))]
#pragma warning disable OPENAI001
[JsonSerializable(typeof(ChatMessageAnnotation))]
#pragma warning restore OPENAI001
[JsonSerializable(typeof(GeekAISearchOption))]
[JsonSerializable(typeof(OpenRouterSearchOption))]
[JsonSerializable(typeof(GoogleSearchPlugin))]
[JsonSerializable(typeof(GeekAISearchOption.GeekAISearchConfig))]
[JsonSerializable(typeof(OpenRouterSearchOption.PluginConfig[]))]
[JsonSerializable(typeof(GeekAIThinkingConfig))]
[JsonSerializable(typeof(OpenRouterReasoningConfig))]
[JsonSerializable(typeof(AdditionalPropertiesDictionary))]
internal partial class LLM_DataSerializeContext : JsonSerializerContext
{
}

public class AdditionalPropertiesConverter
    : JsonConverter<AdditionalPropertiesDictionary>
{
    public override AdditionalPropertiesDictionary? Read(
        ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var objects = JsonSerializer.Deserialize<Dictionary<string, object?>>(
            ref reader, options);
        if (objects == null)
        {
            return null;
        }

        return new AdditionalPropertiesDictionary(objects);
    }

    public override void Write(
        Utf8JsonWriter writer,
        AdditionalPropertiesDictionary value,
        JsonSerializerOptions options)
    {
        // 关键点：复制到普通 Dictionary 再序列化
        var temp = new Dictionary<string, object?>(value, StringComparer.OrdinalIgnoreCase);
        JsonSerializer.Serialize(writer, temp, options);
    }
}