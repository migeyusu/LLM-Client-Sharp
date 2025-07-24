using System.ComponentModel;
using LLMClient.Abstraction;
using LLMClient.UI;
using Microsoft.Extensions.AI;

namespace LLMClient.Endpoints.OpenAIAPI;

public class GeekAISearchService : BaseViewModel, ISearchService
{
    public enum SearchEngineType
    {
        [Description("glm/search-std")] STD,
        [Description("glm/search-pro")] PRO,
        [Description("glm/search-pro-sogou")] PRO_SOGOU,
        [Description("glm/search-pro-quark")] PRO_QUARK,
        [Description("glm/search-pro-jina")] PRO_JINA,
        [Description("glm/search-pro-bing")] PRO_BING,
    }

    public SearchEngineType? SearchEngine { get; set; }

    private bool _searchConfigReturnResult = false;

    public bool ReturnResult
    {
        get => _searchConfigReturnResult;
        set
        {
            if (value == _searchConfigReturnResult) return;
            _searchConfigReturnResult = value;
            OnPropertyChanged();
        }
    }

    public bool CheckAvailable(ILLMModel model)
    {
        if (model.Endpoint is APIEndPoint apiEndPoint && apiEndPoint.ConfigOption.URL == "https://geekai.co/api/v1")
        {
            return true;
        }

        return false;
    }

    public Task ApplySearch(DialogContext context)
    {
        var requestViewItem = context.Request;
        if (requestViewItem == null)
        {
            return Task.CompletedTask;
        }

        requestViewItem.RequestAdditionalProperties ??= new AdditionalPropertiesDictionary();
        requestViewItem.RequestAdditionalProperties["enable_search"] = true;
        if (SearchEngine != null || ReturnResult)
        {
            requestViewItem.RequestAdditionalProperties["search_config"] = new
            {
                search_engine = SearchEngine?.ToString(),
                return_result = ReturnResult
            };
        }

        // 如果没有设置搜索引擎和返回结果，则不需要设置 search_config
        return Task.CompletedTask;
    }
}