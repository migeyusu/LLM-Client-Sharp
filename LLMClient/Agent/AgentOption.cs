using LLMClient.Component.ViewModel.Base;
using LLMClient.Data;

namespace LLMClient.Agent;

public class AgentOption: BaseViewModel
{
    public ParameterizedLLMModelPO? SearchClient
    {
        get;
        set
        {
            if (Equals(value, field)) return;
            field = value;
            OnPropertyChanged();
        }
    }
}