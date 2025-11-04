using System.Diagnostics;
using System.Text.Json.Serialization;
using LLMClient.Abstraction;
using LLMClient.Endpoints.Converters;
using LLMClient.UI;
using LLMClient.UI.Component;
using LLMClient.UI.Component.CustomControl;

namespace LLMClient.Endpoints.OpenAIAPI;

public class OpenRouterSearchOption : BaseViewModel, ISearchOption
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

    [JsonIgnore]
    public ThemedIcon Icon => AsyncThemedIcon.FromUri(new Uri(
        @"pack://application:,,,/LLMClient;component/Resources/Images/openrouter_logo.svg"
        , UriKind.Absolute));

    [JsonIgnore] public string Name => "OpenRouter Search";

    public bool CheckCompatible(ILLMChatClient client)
    {
        return client.Endpoint is APIEndPoint { Option.ModelsSource: ModelSource.OpenRouter };
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
            Trace.WriteLine("SearchPrompt cannot be null or empty");
            requestViewItem.TempAdditionalProperties["plugins"] =
                new PluginConfig[]
                {
                    new PluginConfig()
                    {
                        Id = "web",
                    }
                };
        }
        else
        {
            requestViewItem.TempAdditionalProperties["plugins"] = new PluginConfig[]
            {
                new PluginConfig()
                {
                    Id = "web",
                    MaxResults = MaxResult,
                    SearchPrompt = SearchPrompt,
                }
            };
        }

        return Task.CompletedTask;
    }

    public object Clone()
    {
        return new OpenRouterSearchOption()
        {
            MaxResult = MaxResult,
            SearchPrompt = SearchPrompt,
        };
    }

    public class PluginConfig
    {
        [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;

        [JsonPropertyName("max_results")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? MaxResults { get; set; }

        [JsonPropertyName("search_prompt")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? SearchPrompt { get; set; }
    }
}