using AutoMapper;
using LLMClient.Azure;
using LLMClient.UI;
using Microsoft.Extensions.DependencyInjection;

namespace LLMClient;

public class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        var serviceCollection = new ServiceCollection();
        var collection = serviceCollection.AddSingleton<MainViewModel>()
            .AddSingleton<MainWindow>()
            .AddSingleton<AzureEndPoint>()
            .AddTransient<ModelTypeConverter>()
            .AddTransient<GlobalConfig>()
            .AddSingleton<IEndpointService, EndpointConfigureViewModel>();
        collection.AddAutoMapper(((provider, expression) =>
        {
            expression.CreateMap<DialogModel, DialogViewModel>().ConvertUsing<ModelTypeConverter>();
            expression.CreateMap<DialogViewModel, DialogModel>().ConvertUsing<ModelTypeConverter>();
            expression.CreateMap<AzureOption, AzureEndPoint>();
            expression.ConstructServicesUsing(provider.GetService);
        }), AppDomain.CurrentDomain.GetAssemblies());
        var serviceProvider = collection.BuildServiceProvider();
        App app = new App();
        app.InitializeComponent();
        serviceProvider.GetService<IEndpointService>()?.Initialize();
        app.Run(serviceProvider.GetService<MainWindow>());
    }
}