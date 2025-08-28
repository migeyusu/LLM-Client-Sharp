using LLMClient.Abstraction;
using LLMClient.Endpoints.OpenAIAPI;
using LLMClient.MCP.Servers;
using LLMClient.UI;

namespace LLMClient.MCP;

public class SearchConfigViewModel : BaseViewModel
{
    private bool _searchEnable;

    public bool SearchEnable
    {
        get => _searchEnable;
        set
        {
            if (value == _searchEnable) return;
            _searchEnable = value;
            OnPropertyChanged();
            if (SelectedSearchService == null)
            {
                this.SelectedSearchService = SelectableSearchOptions?.FirstOrDefault();
            }
        }
    }

    private ISearchOption[]? _selectableSearchOptions;
    public ISearchOption[]? SelectableSearchOptions
    {
        get => _selectableSearchOptions;
        set
        {
            if (Equals(value, _selectableSearchOptions)) return;
            _selectableSearchOptions = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsSearchAvailable));
        }
    }

    public bool IsSearchAvailable
    {
        get => _selectableSearchOptions is { Length: > 0 };
    }

    //对于已选中的搜索服务不持久化，因为集合是预设的，而选中项是反序列化获取的，很难对得上实例
    private ISearchOption? _selectedSearchService;
    public ISearchOption? SelectedSearchService
    {
        get => _selectedSearchService;
        set
        {
            if (Equals(value, _selectedSearchService)) return;
            _selectedSearchService = value;
            OnPropertyChanged();
        }
    }
    
    private readonly ISearchOption[] _builtInSearchOptions;

    public void ResetSearch(ILLMChatClient model)
    {
        this.SelectableSearchOptions =
            _builtInSearchOptions.Where(service => service.CheckCompatible(model)).ToArray();
    }

    public ISearchOption? GetUserSearchOption()
    {
        ISearchOption? searchOption = null;
        if (this is { SearchEnable: true, IsSearchAvailable: true })
        {
            var selectedSearchService = this.SelectedSearchService ?? this.SelectableSearchOptions?.FirstOrDefault();
            searchOption = selectedSearchService?.Clone() as ISearchOption;
        }

        return searchOption;
    }

    public SearchConfigViewModel()
    {
        _builtInSearchOptions =
        [
            new GeekAISearchOption(),
            new GoogleSearchPlugin(),
            new OpenRouterSearchOption()
        ];
    }
}