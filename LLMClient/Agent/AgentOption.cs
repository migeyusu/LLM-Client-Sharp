using LLMClient.Data;
using LLMClient.UI.ViewModel.Base;

namespace LLMClient.Agent;

public class AgentOption: BaseViewModel
{
    private LLMClientPersistModel? _searchClient;

    public LLMClientPersistModel? SearchClient
    {
        get => _searchClient;
        set
        {
            if (Equals(value, _searchClient)) return;
            _searchClient = value;
            OnPropertyChanged();
        }
    }
}