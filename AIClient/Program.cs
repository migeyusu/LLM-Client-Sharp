using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using AutoMapper;
using LLMClient.Abstraction;
using LLMClient.Data;
using LLMClient.Endpoints;
using LLMClient.Endpoints.OpenAIAPI;
using LLMClient.Obsolete;
using LLMClient.UI;
using LLMClient.UI.Dialog;
using LLMClient.UI.MCP;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

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
            var collection = serviceCollection.AddSingleton<MainWindowViewModel>()
                .AddSingleton<MainWindow>()
                .AddTransient<AutoMapModelTypeConverter>()
                .AddTransient<GlobalConfig>()
                .AddSingleton<IPromptsResource, PromptsResourceViewModel>()
                .AddSingleton<IEndpointService, EndpointConfigureViewModel>()
                .AddLogging((builder => builder.AddDebug()))
                .AddSingleton<IMcpServiceCollection, McpServiceCollection>();
            collection.AddAutoMapper((provider, expression) =>
            {
                expression.CreateMap<DialogPersistModel, DialogViewModel>().ConvertUsing<AutoMapModelTypeConverter>();
                expression.CreateMap<DialogViewModel, DialogPersistModel>().ConvertUsing<AutoMapModelTypeConverter>();
                expression.CreateMap<APIEndPoint, APIEndPoint>();
                expression.CreateMap<APIDefaultOption, APIDefaultOption>();
                expression.CreateMap<ResponseViewItem, ResponsePersistItem>();
                expression.CreateMap<ResponsePersistItem, ResponseViewItem>().ConvertUsing<AutoMapModelTypeConverter>();
                expression.CreateMap<MultiResponsePersistItem, MultiResponseViewItem>()
                    .ConvertUsing<AutoMapModelTypeConverter>();
                expression.CreateMap<MultiResponseViewItem, MultiResponsePersistItem>()
                    .ConvertUsing<AutoMapModelTypeConverter>();
                // expression.CreateMap<AzureOption, GithubCopilotEndPoint>();
                expression.ConstructServicesUsing(provider.GetService);
            }, AppDomain.CurrentDomain.GetAssemblies());
            
            //debug mode
#if DEBUG
            var resourceBuilder = ResourceBuilder
                .CreateDefault()
                .AddService("TelemetryConsoleQuickstart");
// Enable model diagnostics with sensitive data.
            AppContext.SetSwitch("Microsoft.SemanticKernel.Experimental.GenAI.EnableOTelDiagnosticsSensitive", true);

            using var traceProvider = Sdk.CreateTracerProviderBuilder()
                .SetResourceBuilder(resourceBuilder)
                .AddSource("Microsoft.SemanticKernel*")
                .AddConsoleExporter()
                .Build();

            using var meterProvider = Sdk.CreateMeterProviderBuilder()
                .SetResourceBuilder(resourceBuilder)
                .AddMeter("Microsoft.SemanticKernel*")
                .AddConsoleExporter()
                .Build();
            collection.AddLogging(builder =>
            {
                // Add OpenTelemetry as a logging provider
                builder.AddOpenTelemetry(options =>
                {
                    options.SetResourceBuilder(resourceBuilder);
                    options.AddConsoleExporter();
                    // Format log messages. This is default to false.
                    options.IncludeFormattedMessage = true;
                    options.IncludeScopes = true;
                });
                builder.SetMinimumLevel(LogLevel.Information);
            });
#endif
            serviceProvider = collection.BuildServiceProvider();
            BaseViewModel.ServiceLocator = serviceProvider;
            App app = new App();
            app.InitializeComponent();
            /*var version3Converter = new Version3Converter(serviceProvider);
            version3Converter
                .ConvertToVersion3(
                    "D:\\Dev\\LLM-Client-Sharp\\AIClient\\bin\\Release\\net8.0-windows\\publish\\win-x64\\Dialogs")
                .Wait();
            return;*/
            app.Run(serviceProvider.GetService<MainWindow>());
        }
        catch (Exception e)
        {
            MessageBox.Show("An error occured: " + e + "process will be terminated.");
        }

        try
        {
            var mainViewModel = serviceProvider?.GetService<MainWindowViewModel>();
            if (mainViewModel is { IsInitialized: true })
            {
                mainViewModel.SaveDialogsToLocal().Wait(TimeSpan.FromMinutes(1));
            }

            HttpContentCache.Instance.PersistIndexAsync().Wait(TimeSpan.FromSeconds(5));
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