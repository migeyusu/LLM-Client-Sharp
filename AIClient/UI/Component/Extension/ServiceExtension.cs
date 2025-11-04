using System.Windows.Markup;

namespace LLMClient.UI.Component.Extension;

[MarkupExtensionReturnType(typeof(object))]
public class ServiceExtension : MarkupExtension
{
    public Type? ServiceType { get; set; }

    public ServiceExtension()
    {
    }

    public ServiceExtension(Type serviceType)
    {
        ServiceType = serviceType;
    }

    public override object? ProvideValue(IServiceProvider serviceProvider) // 注意：这里的 serviceProvider 是 WPF 提供的
    {
        if (ServiceType == null)
        {
            return null;
        }

        var appServices = BaseViewModel.ServiceLocator;
        if (appServices == null)
            throw new InvalidOperationException("DI container not available.");

        return appServices.GetService(ServiceType)
               ?? throw new InvalidOperationException($"Service of type {ServiceType} not registered.");
    }
}