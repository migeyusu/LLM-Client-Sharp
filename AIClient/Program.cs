using System.Diagnostics;
using System.Net.Http;
using System.Windows;
using LLMClient.Abstraction;
using LLMClient.Data;
using LLMClient.Endpoints;
using LLMClient.Endpoints.Azure;
using LLMClient.Endpoints.OpenAIAPI;
using LLMClient.UI;
using Microsoft.Extensions.DependencyInjection;

namespace LLMClient;

public class Program
{
    private static Mutex mutex = new Mutex(true, "LLMClient.WPF");

    [STAThread]
    static void Main(string[] args)
    {
        // 尝试获得互斥量的所有权
        if (!Debugger.IsAttached && !mutex.WaitOne(TimeSpan.Zero, true))
        {
            // 如果获取失败，则表示已有实例在运行
            MessageBox.Show("程序已经在运行！", "提示");
            return;
        }

        IServiceProvider? serviceProvider = null;
        try
        {
            var serviceCollection = new ServiceCollection();
            var collection = serviceCollection.AddSingleton<MainViewModel>()
                .AddSingleton<MainWindow>()
                .AddTransient<ModelTypeConverter>()
                .AddTransient<GlobalConfig>()
                .AddSingleton<IPromptsResource, PromptsResourceViewModel>()
                .AddSingleton<IEndpointService, EndpointConfigureViewModel>();
            collection.AddAutoMapper((provider, expression) =>
            {
                expression.CreateMap<DialogPersistModel, DialogViewModel>().ConvertUsing<ModelTypeConverter>();
                expression.CreateMap<DialogViewModel, DialogPersistModel>().ConvertUsing<ModelTypeConverter>();
                expression.CreateMap<APIEndPoint, APIEndPoint>();
                expression.CreateMap<DefaultOption, DefaultOption>();
                expression.CreateMap<ResponseViewItem, ResponsePersistItem>();
                expression.CreateMap<ResponsePersistItem, ResponseViewItem>().ConvertUsing<ModelTypeConverter>();
                expression.CreateMap<MultiResponsePersistItem, MultiResponseViewItem>()
                    .ConvertUsing<ModelTypeConverter>();
                expression.CreateMap<MultiResponseViewItem, MultiResponsePersistItem>()
                    .ConvertUsing<ModelTypeConverter>();
                // expression.CreateMap<AzureOption, GithubCopilotEndPoint>();
                expression.ConstructServicesUsing(provider.GetService);
            }, AppDomain.CurrentDomain.GetAssemblies());
            serviceProvider = collection.BuildServiceProvider();
            App app = new App();
            app.InitializeComponent();
            // SynchronizationHelper.Initialize();
            // app.Run(new TestWindow());
            app.Run(serviceProvider.GetService<MainWindow>());
        }
        catch (Exception e)
        {
            MessageBox.Show("An error occured: " + e + "process will be terminated.");
        }

        try
        {
            var mainViewModel = serviceProvider?.GetService<MainViewModel>();
            if (mainViewModel is { IsInitialized: true })
            {
                mainViewModel.SaveDialogsToLocal().Wait(TimeSpan.FromMinutes(1));
            }
        }
        catch (Exception e)
        {
            Trace.WriteLine(e.Message);
        }
        finally
        {
            if (!Debugger.IsAttached)
            {
                mutex.ReleaseMutex();
            }
        }
    }
}