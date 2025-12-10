using LLMClient.Component.ViewModel.Base;
using Microsoft.Extensions.DependencyInjection;

namespace LLMClient.Component.ViewModel;

public class ViewModelFactory : IViewModelFactory
{
    private readonly IServiceProvider _serviceProvider;

    public ViewModelFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public T CreateViewModel<T>(params object[] args) where T : BaseViewModel
    {
        // 使用 ActivatorUtilities 创建实例
        // args 是特定参数数组，按构造函数顺序传递
        return ActivatorUtilities.CreateInstance<T>(_serviceProvider, args);
    }
}

public interface IViewModelFactory
{
    public T CreateViewModel<T>(params object[] args) where T : BaseViewModel;
}