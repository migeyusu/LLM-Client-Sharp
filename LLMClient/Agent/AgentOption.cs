using LLMClient.Component.ViewModel.Base;
using LLMClient.Data;

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