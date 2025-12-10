using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using LLMClient.Component.ViewModel.Base;

namespace LLMClient.Dialog;

public class PermissionViewModel : BaseViewModel
{
    public string? Title { get; set; }

    public required object Content { get; set; }

    public bool IsCompleted
    {
        get => _isCompleted;
        set
        {
            if (value == _isCompleted) return;
            _isCompleted = value;
            OnPropertyChanged();
        }
    }

    private readonly TaskCompletionSource<bool> _tcs = new TaskCompletionSource<bool>();
    private bool _isCompleted;

    public Task<bool> Task => _tcs.Task;

    public ICommand PermitCommand => new RelayCommand(() =>
    {
        _tcs.TrySetResult(true);
        IsCompleted = true;
    });

    public ICommand RejectCommand => new RelayCommand(() =>
    {
        _tcs.TrySetResult(false);
        IsCompleted = true;
    });
}