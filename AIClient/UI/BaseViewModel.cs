using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Threading;

namespace LLMClient.UI;

public class BaseViewModel : INotifyPropertyChanged
{
    private readonly Lazy<Dispatcher> _dispatcherLazy =
        new Lazy<Dispatcher>((() => Application.Current.Dispatcher), (LazyThreadSafetyMode.PublicationOnly));

    protected Dispatcher Dispatcher => _dispatcherLazy.Value;

    private static IServiceProvider? _serviceProvider;

    /// <summary>
    /// anti-pattern: Service Locator, but useful for this scenario
    /// </summary>
    /// <exception cref="InvalidOperationException"></exception>
    public static IServiceProvider ServiceLocator
    {
        get
        {
            Debug.Assert(_serviceProvider != null, nameof(_serviceProvider) + " != null");
            return _serviceProvider;
        }
        set => _serviceProvider = value;
    }

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
            await Dispatcher.InvokeAsync(() =>
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName)));
        }
    }

    public void Dispatch(Action action)
    {
        if (Dispatcher.CheckAccess())
        {
            action.Invoke();
        }
        else
        {
            Dispatcher.BeginInvoke(action);
        }
    }

    public async Task DispatchAsync(Action action)
    {
        if (!Dispatcher.CheckAccess())
        {
            await Dispatcher.InvokeAsync(action);
        }
    }
}