using System.Text.Json.Serialization;
using OpenAI.Chat;

namespace LLMClient.Data;

[JsonSourceGenerationOptions(WriteIndented = true, GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(DialogFilePersistModel))]
[JsonSerializable(typeof(ProjectPersistModel))]
[JsonSerializable(typeof(DateTimeOffset))]
[JsonSerializable(typeof(string))]         
[JsonSerializable(typeof(int))]           
[JsonSerializable(typeof(List<object>))]
[JsonSerializable(typeof(ChatTokenUsage))]
internal partial class LLM_DataSerializeContext : JsonSerializerContext
{
}