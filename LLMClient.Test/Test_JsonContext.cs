using System.Text.Json.Serialization;
using Microsoft.Extensions.AI;

namespace LLMClient.Test;


[JsonSourceGenerationOptions(WriteIndented = true, GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(FunctionCallContent))]
internal partial class Test_JsonContext: JsonSerializerContext
{
}