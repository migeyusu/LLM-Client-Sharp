using LLMClient.Component.ViewModel.Base;
using LLMClient.Data;

namespace LLMClient.Agent;

public class AgentOption: BaseViewModel
{
    private ParameterizedLLMModelPO? _searchClient;

    public ParameterizedLLMModelPO? SearchClient
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