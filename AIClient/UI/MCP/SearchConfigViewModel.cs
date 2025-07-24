using LLMClient.Abstraction;
using LLMClient.Endpoints.OpenAIAPI;
using LLMClient.UI.MCP.Servers;

namespace LLMClient.UI.MCP;

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
        }
    }

    public ISearchService[]? SelectableSearchFunctions
    {
        get => _selectableSearchFunctions;
        set
        {
            if (Equals(value, _selectableSearchFunctions)) return;
            _selectableSearchFunctions = value;
            OnPropertyChanged();
        }
    }

    //对于已选中的搜索服务不持久化，因为集合是预设的，而选中项是反序列化获取的，很难对得上实例
    public ISearchService? SelectedSearchFunction { get; set; }

    ISearchService[] _builtInSearchServices = new ISearchService[]
    {
        new GeekAISearchService(),
        new GoogleSearchPlugin(),
        new OpenRouterSearchService(),
    };

    private ISearchService[]? _selectableSearchFunctions;

    public void ResetSearchFunction(ILLMModel model)
    {
        this.SelectableSearchFunctions =
            _builtInSearchServices.Where(service => service.CheckCompatible(model)).ToArray();
    }
}