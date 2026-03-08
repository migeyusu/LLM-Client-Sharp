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
    /// 判断异步属性是否已生成值
    /// </summary>
    protected bool IsAsyncPropertyInitialized([CallerMemberName] string? propertyName = null)
    {
        if (string.IsNullOrEmpty(propertyName))
        {
            return false;
        }

        if (_asyncPropertyStates.TryGetValue(propertyName, out var state))
        {
            return state.IsGenerated;
        }

        return false;
    }

    /// <summary>
    /// 等待异步属性生成值。如果属性尚未触发计算，会尝试触发（前提是已注册工厂）。
    /// 如果属性不存在或未关联工厂且未生成值，将一直等待直到被赋值。
    /// </summary>
    protected Task<T> WaitAsyncProperty<T>([CallerMemberName] string? propertyName = null)
    {
        if (string.IsNullOrEmpty(propertyName))
        {
            return Task.FromResult(default(T)!);
        }

        var state = (AsyncPropertyState<T>)_asyncPropertyStates.GetOrAdd(
            propertyName,
            _ => new AsyncPropertyState<T>(null, default!, this, propertyName));

        return state.WaitAsync();
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
        bool IsGenerated { get; }
    }

    private class AsyncPropertyState<T> : IAsyncPropertyState
    {
        private T _value;
        private readonly T _defaultValue;
        private readonly BaseViewModel _viewModel;
        private readonly string _propertyName;
        
        // 核心状态标志：明确区分"是否有值"，不再依赖 value != defaultValue
        private bool _isLoaded;
        // 标记计算是否正在进行，防止重复触发
        private bool _isLoading;
        
        // 核心等待机制：用于 WaitAsync
        private TaskCompletionSource<T> _tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
        
        // 计算工厂
        private Func<Task<T>>? _calculateAsync;
        private readonly SemaphoreSlim _semaphore = new(1, 1);

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
            _isLoaded = false;
        }

        public void EnsureFactoryAttached(Func<Task<T>> factory)
        {
            // 只有当工厂为空时才赋值，避免覆盖
            if (_calculateAsync == null)
            {
                _calculateAsync = factory;
            }
        }

        public bool IsGenerated => _isLoaded;

        public T GetValue()
        {
            // 1. 如果已加载，直接返回
            if (_isLoaded) return _value;

            // 2. 尝试触发加载（惰性）
            TriggerLoading();

            // 3. 返回默认值（此时还在加载中或无法加载）
            return _value;
        }

        public bool SetValue(T newValue)
        {
            // 如果已加载且值未变，则无需更新
            // 注意：如果之前是未加载状态(_isLoaded=false)，即使 newValue 等于 _defaultValue，
            // 我们也要执行 Set 逻辑，将其标记为 _isLoaded=true。
            if (_isLoaded && EqualityComparer<T>.Default.Equals(_value, newValue))
            {
                return false;
            }

            _value = newValue;
            _isLoaded = true;
            // Set 操作视为加载结束（或覆盖了正在进行的加载结果）
            _isLoading = false; 

            // 通知所有等待该属性的任务
            _tcs.TrySetResult(newValue);

            _viewModel.OnPropertyChanged(_propertyName);
            return true;
        }

        public Task<T> WaitAsync()
        {
            // 如果已经 Loaded，TCS 应该已经是 Completed 状态，可以直接 await 它
            // 如果没 Loaded，尝试触发加载，然后返回 TCS 让调用者等待
            TriggerLoading();
            return _tcs.Task;
        }

        public void Invalidate()
        {
            _value = _defaultValue;
            _isLoading = false;
            _isLoaded = false;

            // 关键：如果 TCS 已经完结（说明之前有过值），现在变为无效
            // 需要重置 TCS，以便下一次 WaitAsync 可以等待新的计算结果
            if (_tcs.Task.IsCompleted)
            {
                _tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
            }

            _viewModel.OnPropertyChangedAsync(_propertyName);
        }

        private void TriggerLoading()
        {
            // 如果已经加载、正在加载或没有工厂，则不执行
            if (_isLoaded || _isLoading || _calculateAsync == null) return;

            _isLoading = true;
            
            // Fire and forget, 但在 UI 线程处理回调
            _ = CalculateAsyncInternal();
        }

        private async Task CalculateAsyncInternal()
        {
            await _semaphore.WaitAsync();
            try
            {
                //再次检查状态：如果等待锁的过程中被 Loaded (Set) 了，或者工厂没了，就退出
                if (_isLoaded || _calculateAsync == null)
                    return;

                var result = await _calculateAsync();

                // 切换回 UI 线程更新状态
                await _viewModel.DispatchAsync(() =>
                {
                    // 最终检查：如果在计算过程中被 SetValue 抢占了，则丢弃本次计算结果
                    if (_isLoaded) return;

                    _value = result;
                    _isLoaded = true;
                    _isLoading = false;

                    // 完成 TCS，唤醒 WaitAsync
                    _tcs.TrySetResult(result);

                    _viewModel.OnPropertyChanged(_propertyName);
                });
            }
            catch (Exception ex)
            {
                // 计算失败，是否需要重置 _isLoading 允许重试？
                // 或者将异常传给 TCS？
                // 这里选择重置 Loading 状态并在 Debug 输出，避免应用程序崩溃
                _isLoading = false;
                Debug.WriteLine($"Error calculating async property {_propertyName}: {ex}");
                // _tcs.TrySetException(ex); // 可选：让等待者知道出错了
            }
            finally
            {
                _semaphore.Release();
            }
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