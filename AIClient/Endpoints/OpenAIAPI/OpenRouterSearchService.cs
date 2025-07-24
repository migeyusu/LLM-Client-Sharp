using System.Windows.Media;
using LLMClient.Abstraction;
using LLMClient.Data;
using LLMClient.UI;
using LLMClient.UI.Component;
using Microsoft.Extensions.AI;

namespace LLMClient.Endpoints.OpenAIAPI;

public class OpenRouterSearchService : BaseViewModel, ISearchService
{
    private int _maxResult = 5;
    private string? _searchPrompt;

    public int MaxResult
    {
        get => _maxResult;
        set
        {
            if (value == _maxResult) return;
            _maxResult = value;
            OnPropertyChanged();
        }
    }

    public string? SearchPrompt
    {
        get => _searchPrompt;
        set
        {
            if (value == _searchPrompt) return;
            _searchPrompt = value;
            OnPropertyChanged();
        }
    }
    public string GetUniqueId()
    {
        return "MaxResult:" + MaxResult + ",SearchPrompt:" + SearchPrompt;
    }

    public ThemedIcon Icon => AsyncThemedIcon.FromUri(new Uri(
        @"pack://application:,,,/LLMClient;component/Resources/Images/openrouter_logo.svg"
        , UriKind.Absolute));

    public string Name => "OpenRouter Search";

    public bool CheckCompatible(ILLMModel model)
    {
        if (model.Endpoint is APIEndPoint apiEndPoint && apiEndPoint.ConfigOption.URL == "https://openrouter.ai/api/v1")
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

        if (string.IsNullOrEmpty(SearchPrompt))
        {
            throw new InvalidOperationException("SearchPrompt cannot be null or empty");
        }

        requestViewItem.RequestAdditionalProperties ??= new AdditionalPropertiesDictionary();
        requestViewItem.RequestAdditionalProperties["plugins"] = $@"
        [
            {{
                ""id"": ""web"",
                ""max_results"": {MaxResult},
                ""search_prompt"": ""{SearchPrompt}""
            }}
        ]";
        return Task.CompletedTask;
    }

    public object Clone()
    {
        return new OpenRouterSearchService()
        {
            MaxResult = MaxResult,
            SearchPrompt = SearchPrompt,
        };
    }
}