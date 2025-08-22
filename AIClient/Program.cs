using System.Diagnostics;
using System.IO;
using System.Windows;
using LLMClient.Abstraction;
using LLMClient.Data;
using LLMClient.Endpoints;
using LLMClient.MCP;
using LLMClient.MCP.Servers;
using LLMClient.Rag;
using LLMClient.UI;
using LLMClient.UI.Log;
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
    private static readonly Mutex Mutex = new Mutex(true, "LLMClient.WPF");

    [STAThread]
    static void Main(string[] args)
    {
        // 尝试获得互斥量的所有权
        if (!Debugger.IsAttached && !Mutex.WaitOne(TimeSpan.Zero, true))
        {
            // 如果获取失败，则表示已有实例在运行
            MessageBox.Show("程序已经在运行！", "提示");
            return;
        }

        IServiceProvider? serviceProvider = null;
        var tempPath = new DirectoryInfo(Extension.TempPath);
        try
        {
            var serviceCollection = new ServiceCollection();
            var collection = serviceCollection.AddSingleton<MainWindowViewModel>()
                .AddSingleton<MainWindow>()
                .AddTransient<AutoMapModelTypeConverter>()
                .AddSingleton<IPromptsResource, PromptsResourceViewModel>()
                .AddSingleton<IEndpointService, EndpointConfigureViewModel>()
                .AddSingleton<IRagSourceCollection, RagSourceCollection>()
#if DEBUG
                .AddLogging(builder => builder.AddDebug())
#endif
                .AddSingleton<IMcpServiceCollection, McpServiceCollection>()
                .AddSingleton<IBuiltInFunctionsCollection, BuiltInFunctionsCollection>()
                .AddMap();
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
            if (!tempPath.Exists)
            {
                tempPath.Create();
            }

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
        finally
        {
            if (!Debugger.IsAttached)
            {
                Mutex.ReleaseMutex();
            }

            // tempPath.Delete(true);//默认不删除
        }
    }
}