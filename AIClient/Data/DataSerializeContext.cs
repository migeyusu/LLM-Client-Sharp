using System.Text.Json.Serialization;
using LLMClient.UI;

namespace LLMClient.Data;

[JsonSourceGenerationOptions(WriteIndented = true, GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(DialogFilePersistModel))]
[JsonSerializable(typeof(ProjectPersistModel))]
internal partial class LLM_DataSerializeContext : JsonSerializerContext
{
}