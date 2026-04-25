using LLMClient.Agent.MiniSWE;
using LLMClient.Component.ViewModel.Base;

namespace LLMClient.Dialog;

public class AgentOption : BaseViewModel
{
    public string? WorkingDirectory
    {
        get;
        set
        {
            if (value == field) return;
            field = value;
            OnPropertyChanged();
        }
    }

    public RunPlatform Platform
    {
        get;
        set
        {
            if (value == field) return;
            field = value;
            OnPropertyChanged();
        }
    }
}