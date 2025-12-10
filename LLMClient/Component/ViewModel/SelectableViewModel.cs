using LLMClient.Component.ViewModel.Base;

namespace LLMClient.Component.ViewModel;

public class SelectableViewModel<T> : BaseViewModel
{
    public SelectableViewModel(T data)
    {
        Data = data;
    }

    private bool _isSelected;

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