using System.ComponentModel;
using System.Text.Json.Serialization;
using LLMClient.Abstraction;
using LLMClient.Component.CustomControl;
using LLMClient.Component.ViewModel.Base;
using LLMClient.Endpoints.Converters;

namespace LLMClient.Endpoints.OpenAIAPI;

public class GeekAISearchOption : BaseViewModel, ISearchOption
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

    [JsonIgnore] public string Name => "GeekAI Search Service";

    public string GetUniqueId()
    {
        return "ReturnResult:" + ReturnResult + ",SearchEngine:" + SearchEngine;
    }

    [JsonIgnore]
    public ThemedIcon Icon =>
        AsyncThemedIcon.FromUri(new Uri("pack://application:,,,/LLMClient;component/Resources/Images/geekai.png"));

    public bool CheckCompatible(ILLMChatClient client)
    {
        return client.Endpoint is APIEndPoint { Option.ModelsSource: ModelSource.GeekAI };
    }

    public Task ApplySearch(DialogContext context)
    {
        var requestViewItem = context.Request;
        if (requestViewItem == null)
        {
            return Task.CompletedTask;
        }

        requestViewItem.TempAdditionalProperties["enable_search"] = true;
        if (SearchEngine != null)
        {
            requestViewItem.TempAdditionalProperties["search_config"] = new GeekAISearchConfig()
            {
                Engine = SearchEngine.GetEnumDescription(),
                ReturnResult = ReturnResult
            };
        }

        // 如果没有设置搜索引擎和返回结果，则不需要设置 search_config
        return Task.CompletedTask;
    }

    public object Clone()
    {
        return new GeekAISearchOption()
        {
            SearchEngine = this.SearchEngine,
            ReturnResult = this.ReturnResult
        };
    }

    public class GeekAISearchConfig
    {
        [JsonPropertyName("engine")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Engine { get; set; }

        [JsonPropertyName("return_result")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public bool ReturnResult { get; set; } = false;
    }
}