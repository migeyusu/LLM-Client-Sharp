using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Threading;

namespace LLMClient.Component.ViewModel.Base;

public class BaseViewModel : INotifyPropertyChanged
{
    private readonly Lazy<Dispatcher> _dispatcherLazy = new(() =>
    {
        var appDispatcher = Application.Current?.Dispatcher;
        if (appDispatcher != null)
        {
            return appDispatcher;
        }

        return Dispatcher.CurrentDispatcher;
    }, LazyThreadSafetyMode.PublicationOnly);

    private Dispatcher Dispatcher => _dispatcherLazy.Value;

    private readonly ConcurrentDictionary<string, IAsyncPropertyState> _asyncPropertyStates = new();

    /// <summary>
    /// 获取异步属性值。如果之前通过Set赋值但未提供工厂，此处会自动附加工厂方法。
    /// </summary>
    protected T GetAsyncProperty<T>(
        Func<Task<T>> calculateAsync,
        T defaultValue = default(T),
        [CallerMemberName] string? propertyName = null)
    {
        if (string.IsNullOrEmpty(propertyName))
        {
            return defaultValue;
        }

        // 获取或创建状态（如果是由Set创建的，CreateFunc不会执行）
        var state = (AsyncPropertyState<T>)_asyncPropertyStates.GetOrAdd(
            propertyName,
            _ => new AsyncPropertyState<T>(calculateAsync, defaultValue, this, propertyName));

        // 如果是先Set后Get，此时state内的Factory可能是null，需要补上
        state.EnsureFactoryAttached(calculateAsync);
        return state.GetValue();
    }

    /// <summary>
    /// 设置异步属性值。支持在没有工厂方法的情况下初始化值（如DTO映射）。
    /// </summary>
    protected bool SetAsyncProperty<T>(
        T value, T defaultValue = default(T),
        [CallerMemberName] string? propertyName = null)
    {
        if (string.IsNullOrEmpty(propertyName))
        {
            return false;
        }

        // 获取或创建状态。注意：这里传入 null 作为工厂方法，因为Set不应关心计算逻辑
        var state = (AsyncPropertyState<T>)_asyncPropertyStates.GetOrAdd(
            propertyName,
            _ => new AsyncPropertyState<T>(null, defaultValue, this, propertyName));

        return state.SetValue(value);
    }

    /// <summary>
    /// 使异步属性失效，触发重新计算
    /// </summary>
    public void InvalidateAsyncProperty([CallerMemberName] string? propertyName = null)
    {
        if (string.IsNullOrEmpty(propertyName))
        {
            return;
        }

        if (_asyncPropertyStates.TryGetValue(propertyName, out var state))
        {
            state.Invalidate();
        }
    }

    private interface IAsyncPropertyState
    {
        void Invalidate();
    }

    private class AsyncPropertyState<T> : IAsyncPropertyState
    {
        private T _value;

        // 移除 readonly，允许迟绑定
        private Func<Task<T>>? _calculateAsync;
        private readonly SemaphoreSlim _semaphore = new(1, 1);
        private readonly T _defaultValue;
        private readonly BaseViewModel _viewModel;
        private readonly string _propertyName;
        private readonly bool _isValueType;

        public AsyncPropertyState(
            Func<Task<T>>? calculateAsync,
            T defaultValue,
            BaseViewModel viewModel,
            string propertyName)
        {
            _calculateAsync = calculateAsync;
            _defaultValue = defaultValue;
            _viewModel = viewModel;
            _propertyName = propertyName;
            _value = defaultValue;

            // 优化：缓存类型判断结果
            _isValueType = typeof(T).IsValueType;
        }

        /// <summary>
        /// 确保计算方法已关联（解决先Set后Get导致工厂丢失的问题）
        /// </summary>
        public void EnsureFactoryAttached(Func<Task<T>> factory)
        {
            if (_calculateAsync == null)
            {
                // 原子操作赋值，无需加锁（引用赋值是原子的）
                _calculateAsync = factory;
            }
        }

        public T GetValue()
        {
            // 如果已经被赋值（非默认值），直接返回，不触发计算
            if (!IsDefaultValue(_value))
                return _value;

            // 尝试触发计算（如果缺少工厂方法则跳过，等待下次Get提供）
            if (_calculateAsync != null && _semaphore.CurrentCount > 0)
            {
                _ = CalculateAsync();
            }

            return _value;
        }

        public bool SetValue(T newValue)
        {
            // 对比是否变化
            if (EqualityComparer<T>.Default.Equals(_value, newValue))
                return false;
            // 这里不需要异步锁，因为这是UI/主线程直接设置值
            // 我们假设Setter主要由UI线程或初始化代码调用
            // 如果需要支持多线程并发Set，才需要加锁
            _value = newValue;
            // 触发通知
            _viewModel.OnPropertyChanged(_propertyName);
            return true;
        }

        private async Task CalculateAsync()
        {
            await _semaphore.WaitAsync();
            try
            {
                // 双重检查
                if (!IsDefaultValue(_value) || _calculateAsync == null)
                    return;

                var newValue = await _calculateAsync();
                // 再次检查以防止计算过程中被Set了新值
                await _viewModel.DispatchAsync(() => UpdateValue(newValue));
            }
            finally
            {
                _semaphore.Release();
            }
        }

        private void UpdateValue(T newValue)
        {
            // 如果在计算过程中，外部已经Set了有效值，则放弃本次计算结果（以最新Set为准）
            if (!IsDefaultValue(_value)) return;
            _value = newValue;
            _viewModel.OnPropertyChanged(_propertyName);
        }

        public void Invalidate()
        {
            _value = _defaultValue;
            // 触发通知，让UI重新Get，从而触发CalculateAsync
            _viewModel.OnPropertyChangedAsync(_propertyName);
        }

        private bool IsDefaultValue(T value)
        {
            if (_isValueType)
                return EqualityComparer<T>.Default.Equals(value, _defaultValue);
            return value == null || EqualityComparer<T>.Default.Equals(value, _defaultValue);
        }
    }

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
        if (PropertyChanged == null || string.IsNullOrEmpty(propertyName))
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

    public bool PublicEquals(T? other)
    {
        if (other == null) return false;
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