using System.Text.Json.Serialization;
using LLMClient.Endpoints.OpenAIAPI;
using LLMClient.UI.MCP.Servers;
using OpenAI.Chat;

namespace LLMClient.Data;

[JsonSourceGenerationOptions(WriteIndented = true, GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(DialogFilePersistModel))]
[JsonSerializable(typeof(ProjectPersistModel))]
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
[JsonSerializable(typeof(ChatMessageAnnotation))]
[JsonSerializable(typeof(GeekAISearchService))]
[JsonSerializable(typeof(OpenRouterSearchService))]
[JsonSerializable(typeof(GoogleSearchPlugin))]
[JsonSerializable(typeof(GeekAISearchService.SearchConfig))]
[JsonSerializable(typeof(OpenRouterSearchService.PluginConfig[]))]
internal partial class LLM_DataSerializeContext : JsonSerializerContext
{
}