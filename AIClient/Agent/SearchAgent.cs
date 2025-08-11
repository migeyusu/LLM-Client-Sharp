using System.Text.Json.Serialization;
using LLMClient.Abstraction;
using LLMClient.UI.Component;

namespace LLMClient.Agent;

/**
 * search agent 会自动根据内容产生搜索请求，并将结果添加到对话中。
 */
public class SearchAgent : IAgent, ISearchService
{
    [JsonIgnore]
    public string Name => "Search Agent";
    
    public string GetUniqueId()
    {
        return "search-agent";
    }

    [JsonIgnore]
    public ThemedIcon Icon => ThemedIcon.EmptyIcon;

    public bool CheckCompatible(ILLMChatClient client)
    {
        return true;
    }

    public Task ApplySearch(DialogContext context)
    {
        throw new NotSupportedException();
    }

    public object Clone()
    {
        throw new NotImplementedException();
    }
}