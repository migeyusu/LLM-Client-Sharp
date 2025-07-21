using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using LLMClient.UI;
using Microsoft.Extensions.AI;

namespace LLMClient.Data;

[JsonSourceGenerationOptions(WriteIndented = true, GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(DialogFilePersistModel))]
[JsonSerializable(typeof(ProjectPersistModel))]
internal partial class LLM_DataSerializeContext : JsonSerializerContext
{
}