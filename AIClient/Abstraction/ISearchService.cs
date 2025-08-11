using System.Text.Json.Serialization;
using LLMClient.Endpoints.OpenAIAPI;
using LLMClient.MCP.Servers;
using LLMClient.UI.Component;

namespace LLMClient.Abstraction;

[JsonDerivedType(typeof(OpenRouterSearchService), "openrouter")]
[JsonDerivedType(typeof(GeekAISearchService), "geekai")]
[JsonDerivedType(typeof(GoogleSearchPlugin), "google-search-plugin")]
public interface ISearchService : ICloneable
{
    string Name { get; }

    string GetUniqueId();

    [JsonIgnore] ThemedIcon Icon { get; }

    bool CheckCompatible(ILLMChatClient client);

    Task ApplySearch(DialogContext context);
}