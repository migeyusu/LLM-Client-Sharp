using System.Text.Json.Serialization;
using LLMClient.Abstraction;
using LLMClient.Data;
using LLMClient.UI.Component;
using MaterialDesignThemes.Wpf;
using Microsoft.SemanticKernel.Data;

namespace LLMClient.Agent;

/// <summary>
/// search agent 会自动根据内容产生搜索请求，并将结果添加到对话中。
/// <para>目的是节约tokens消耗</para>
/// </summary>
public abstract class SearchAgent : IAgent, ISearchOption
{
    [JsonIgnore] public abstract string Name { get; }

    public abstract string GetUniqueId();

    [JsonIgnore]
    public virtual ThemedIcon Icon => new LocalThemedIcon(PackIconKind.HeadDotsHorizontalOutline.ToImageSource());

    public bool CheckCompatible(ILLMChatClient client)
    {
        return true;
    }

    public abstract Task ApplySearch(DialogContext context);

    public abstract object Clone();
}

public class DeepResearchAgent : SearchAgent
{
    private SearchAgent _searchAgent;

    public DeepResearchAgent(SearchAgent searchAgent)
    {
        _searchAgent = searchAgent;
    }

    public override string Name { get; }

    public override string GetUniqueId()
    {
        throw new NotImplementedException();
    }

    public override Task ApplySearch(DialogContext context)
    {
        throw new NotImplementedException();
    }

    public override object Clone()
    {
        throw new NotImplementedException();
    }
}

public class SKSearchAgent : SearchAgent
{
    private ITextSearch textSearch;

    public SKSearchAgent(ITextSearch textSearch)
    {
        this.textSearch = textSearch;
    }

    public override string Name { get; }

    public override string GetUniqueId()
    {
        throw new NotImplementedException();
    }

    public override Task ApplySearch(DialogContext context)
    {
        throw new NotImplementedException();
    }

    public override object Clone()
    {
        throw new NotImplementedException();
    }
}

public class InternalSearchAgent : SearchAgent
{
    public override string Name { get; }

    private ISearchService _searchService;

    public InternalSearchAgent(ISearchService searchService)
    {
        this._searchService = searchService;
    }

    public override string GetUniqueId()
    {
        throw new NotImplementedException();
    }

    public override Task ApplySearch(DialogContext context)
    {
        throw new NotImplementedException();
    }

    public override object Clone()
    {
        throw new NotImplementedException();
    }
}