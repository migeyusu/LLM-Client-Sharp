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

    public T Create<T>(params object[] args) where T : class
    {
        return ActivatorUtilities.CreateInstance<T>(_serviceProvider, args);
    }

    public T CreateViewModel<T>(params object[] args) where T : BaseViewModel
    {
        return Create<T>(args);
    }
}

public interface IViewModelFactory
{
    public T Create<T>(params object[] args) where T : class;

    public T CreateViewModel<T>(params object[] args) where T : BaseViewModel;
}