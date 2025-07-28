using System.Text.Json.Serialization;
using LLMClient.Endpoints.OpenAIAPI;
using LLMClient.UI.Component;
using LLMClient.UI.MCP.Servers;

namespace LLMClient.Abstraction;

[JsonDerivedType(typeof(OpenRouterSearchService), "openrouter")]
[JsonDerivedType(typeof(GeekAISearchService), "geekai")]
[JsonDerivedType(typeof(GoogleSearchPlugin), "google-search-plugin")]
public interface ISearchService : ICloneable
{
    string Name { get; }

    string GetUniqueId();

    [JsonIgnore] ThemedIcon Icon { get; }

    bool CheckCompatible(ILLMModel model);

    Task ApplySearch(DialogContext context);
}