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
                this.SelectedSearchService = SelectableSearchServices?.FirstOrDefault();
            }
        }
    }

    public ISearchService[]? SelectableSearchServices
    {
        get => _selectableSearchServices;
        set
        {
            if (Equals(value, _selectableSearchServices)) return;
            _selectableSearchServices = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsSearchAvailable));
        }
    }

    public bool IsSearchAvailable
    {
        get => _selectableSearchServices is { Length: > 0 };
    }

    //对于已选中的搜索服务不持久化，因为集合是预设的，而选中项是反序列化获取的，很难对得上实例
    public ISearchService? SelectedSearchService
    {
        get => _selectedSearchService;
        set
        {
            if (Equals(value, _selectedSearchService)) return;
            _selectedSearchService = value;
            OnPropertyChanged();
        }
    }

    private readonly ISearchService[] _builtInSearchServices;

    private ISearchService[]? _selectableSearchServices;
    private ISearchService? _selectedSearchService;

    public void ResetSearchFunction(ILLMChatClient model)
    {
        this.SelectableSearchServices =
            _builtInSearchServices.Where(service => service.CheckCompatible(model)).ToArray();
    }

    public ISearchService? GetUserSearchService()
    {
        ISearchService? searchService = null;
        if (this is { SearchEnable: true, IsSearchAvailable: true })
        {
            var selectedSearchService = this.SelectedSearchService ?? this.SelectableSearchServices?.FirstOrDefault();
            searchService = selectedSearchService?.Clone() as ISearchService;
        }

        return searchService;
    }

    public SearchConfigViewModel()
    {
        _builtInSearchServices =
        [
            new GeekAISearchService(),
            new GoogleSearchPlugin(),
            new OpenRouterSearchService()
        ];
    }
}