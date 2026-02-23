using System.Text.Json.Serialization;
using LLMClient.Component.CustomControl;
using LLMClient.Endpoints.OpenAIAPI;
using LLMClient.ToolCall.DefaultPlugins;

namespace LLMClient.Abstraction;

/// <summary>
/// 搜索配置/选项。是一种抽象程度较高的搜索服务。
/// <para>可以是一个具体的搜索函数给LLM调用，也可以是Search Agent用于补充上下文。</para>
/// </summary>
[JsonDerivedType(typeof(OpenRouterSearchOption), "openrouter")]
[JsonDerivedType(typeof(GeekAISearchOption), "geekai")]
[JsonDerivedType(typeof(GoogleSearchPlugin), "google-search-plugin")]
// [JsonDerivedType(typeof(SearchAgent), "search-agent")]
public interface ISearchOption : ICloneable
{
    string Name { get; }

    string GetUniqueId();

    [JsonIgnore] ThemedIcon Icon { get; }

    bool CheckCompatible(ILLMChatClient client);

    Task ApplySearch(DialogContext context);
}