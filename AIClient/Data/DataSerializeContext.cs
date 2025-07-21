using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using LLMClient.UI;
using Microsoft.Extensions.AI;

namespace LLMClient.Data;

[JsonSourceGenerationOptions(WriteIndented = true, GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(DialogFilePersistModel))]
[JsonSerializable(typeof(ProjectPersistModel))]
[JsonSerializable(typeof(DateTimeOffset))]
[JsonSerializable(typeof(string))]         
[JsonSerializable(typeof(int))]           
[JsonSerializable(typeof(List<object>))]
internal partial class LLM_DataSerializeContext : JsonSerializerContext
{
}