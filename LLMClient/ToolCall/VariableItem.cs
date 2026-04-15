using LLMClient.Component.ViewModel.Base;

namespace LLMClient.ToolCall;

public class VariableItem : BaseViewModel
{
    public string? Name
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged();
            }
        }
    }

    public string? Value
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged();
            }
        }
    }
}