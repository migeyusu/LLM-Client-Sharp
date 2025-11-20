using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Threading;

namespace LLMClient.UI.ViewModel.Base;

public class BaseViewModel : INotifyPropertyChanged
{
    private readonly Lazy<Dispatcher> _dispatcherLazy =
        new Lazy<Dispatcher>((() =>
        {
            var appDispatcher = Application.Current?.Dispatcher;
            if (appDispatcher != null)
            {
                return appDispatcher;
            }

            return Dispatcher.CurrentDispatcher;
        }), (LazyThreadSafetyMode.PublicationOnly));

    private Dispatcher Dispatcher => _dispatcherLazy.Value;

    private static IServiceProvider? _serviceProvider;

    /// <summary>
    /// anti-pattern: Service Locator, but works well in this scenario
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
        if (PropertyChanged == null)
        {
            return;
        }

        await DispatchAsync(() => OnPropertyChanged(propertyName));
    }

    protected void Dispatch(Action action)
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

    protected async Task DispatchAsync(Action action)
    {
        if (!Dispatcher.CheckAccess())
        {
            await Dispatcher.InvokeAsync(action);
        }
        else
        {
            action.Invoke();
        }
    }
}

public class BaseViewModel<T> : BaseViewModel where T : class
{
    private PropertyInfo[]? _publicProperties;

    public bool PublicEquals(T other)
    {
        if (ReferenceEquals(this, other)) return true;
        if (GetType() != other.GetType()) return false;

        _publicProperties ??= GetType()
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead)
            .ToArray();

        foreach (var publicProperty in _publicProperties)
        {
            var value = publicProperty.GetValue(this);
            var comparedValue = publicProperty.GetValue(other);
            if (value == null && comparedValue == null) continue;
            if (value == null || comparedValue == null) return false;

            // 检查是否是BaseViewModel<T>的子类
            if (IsBaseViewModelType(value.GetType()) && IsBaseViewModelType(comparedValue.GetType()))
            {
                // 如果是BaseViewModel<T>类型，调用PublicEquals方法
                var method = value.GetType().GetMethod("PublicEquals");
                if (method != null)
                {
                    var result = (bool?)method.Invoke(value, new[] { comparedValue });
                    if (result == false) return false;
                }
                else
                {
                    // 如果没有PublicEquals方法，使用默认比较
                    if (!value.Equals(comparedValue)) return false;
                }
            }
            else
            {
                // 普通类型比较
                if (!value.Equals(comparedValue)) return false;
            }
        }

        return true;
    }

    private bool IsBaseViewModelType(Type type)
    {
        // 检查类型是否继承自BaseViewModel<T>
        var currentType = type;
        while (currentType != null)
        {
            if (currentType.IsGenericType &&
                currentType.GetGenericTypeDefinition() == typeof(BaseViewModel<>))
            {
                return true;
            }

            currentType = currentType.BaseType;
        }

        return false;
    }
}