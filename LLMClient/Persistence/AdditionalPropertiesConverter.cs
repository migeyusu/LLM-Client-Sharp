using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.AI;

namespace LLMClient.Persistence;

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