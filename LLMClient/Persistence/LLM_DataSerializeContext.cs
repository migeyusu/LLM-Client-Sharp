using System.Text.Json.Serialization;
using LLMClient.Configuration;
using LLMClient.Endpoints;
using LLMClient.Endpoints.OpenAIAPI;
using LLMClient.ToolCall;
using LLMClient.ToolCall.DefaultPlugins;
using Microsoft.Extensions.AI;
using OpenAI.Chat;

namespace LLMClient.Persistence;

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
[JsonSerializable(typeof(Dictionary<string, long>))]
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
[JsonSerializable(typeof(AIFunctionGroupPersistObject))]
[JsonSerializable(typeof(AIFunctionGroupDefinitionPersistModel))]
[JsonSerializable(typeof(StdIOServerItemPersistModel))]
[JsonSerializable(typeof(SseServerItemPersistModel))]
[JsonSerializable(typeof(FileSystemPluginPersistModel))]
[JsonSerializable(typeof(WslCliPluginPersistModel))]
[JsonSerializable(typeof(WinCliPluginPersistModel))]
[JsonSerializable(typeof(GoogleSearchPluginPersistModel))]
[JsonSerializable(typeof(UrlFetcherPluginPersistModel))]
[JsonSerializable(typeof(ProjectAwarenessPluginPersistModel))]
[JsonSerializable(typeof(SymbolSemanticPluginPersistModel))]
[JsonSerializable(typeof(CodeSearchPluginPersistModel))]
[JsonSerializable(typeof(CodeReadingPluginPersistModel))]
[JsonSerializable(typeof(InspectAgentPersistModel))]
[JsonSerializable(typeof(PlannerAgentPersistModel))]
[JsonSerializable(typeof(SummaryAgentPersistModel))]
[JsonSerializable(typeof(NvidiaResearchClientPersistModel))]
[JsonSerializable(typeof(VariableItem))]
[JsonSerializable(typeof(VariableItem[]))]
[JsonSerializable(typeof(ProxySetting))]
[JsonSerializable(typeof(ProxyOption))]
[JsonSerializable(typeof(GeekAISearchOption.GeekAISearchConfig))]
[JsonSerializable(typeof(OpenRouterSearchOption.PluginConfig[]))]
[JsonSerializable(typeof(GeekAIThinkingConfig))]
[JsonSerializable(typeof(OpenRouterReasoningConfig))]
[JsonSerializable(typeof(AdditionalPropertiesDictionary))]
internal partial class LLM_DataSerializeContext : JsonSerializerContext
{
}