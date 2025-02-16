using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Threading;

namespace LLMClient.UI;

public class BaseViewModel : INotifyPropertyChanged
{
    private readonly Lazy<Dispatcher> _dispatcherLazy =
        new Lazy<Dispatcher>((() => Application.Current.Dispatcher), (LazyThreadSafetyMode.PublicationOnly));

    private Dispatcher Dispatcher => _dispatcherLazy.Value;

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    protected virtual async void OnPropertyChangedAsync([CallerMemberName] string? propertyName = null)
    {
        if (Dispatcher.CheckAccess())
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        else
        {
            await Dispatcher.InvokeAsync(
                () => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName)));
        }
    }
}