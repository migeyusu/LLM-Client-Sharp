using LLMClient.Component.ViewModel.Base;

namespace LLMClient.ToolCall;

public class VariableItem : BaseViewModel
{
    private string? _name;
    private string? _value;

    public string? Name
    {
        get => _name;
        set
        {
            if (_name != value)
            {
                _name = value;
                OnPropertyChanged();
            }
        }
    }

    public string? Value
    {
        get => _value;
        set
        {
            if (_value != value)
            {
                _value = value;
                OnPropertyChanged();
            }
        }
    }
}