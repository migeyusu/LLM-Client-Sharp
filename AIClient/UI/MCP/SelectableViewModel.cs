namespace LLMClient.UI.MCP;

public class SelectableViewModel<T> : BaseViewModel
{
    private bool _isSelected;

    public SelectableViewModel(T data)
    {
        Data = data;
    }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (value == _isSelected) return;
            _isSelected = value;
            OnPropertyChanged();
        }
    }

    public T Data { get; set; }
}