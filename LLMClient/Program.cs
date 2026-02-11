using System.Diagnostics;
using System.Windows;
using LLMClient.Abstraction;
using LLMClient.Component;
using LLMClient.Component.ViewModel;
using LLMClient.Component.ViewModel.Base;
using LLMClient.Configuration;
using LLMClient.ContextEngineering;
using LLMClient.Data;
using LLMClient.Endpoints;
using LLMClient.Rag;
using LLMClient.Research;
using LLMClient.ToolCall;
using LLMClient.ToolCall.Servers;
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
        MainWindow? mainWindow = null;
        var tempPath = new DirectoryInfo(Extension.TempPath);
        if (!tempPath.Exists)
        {
            tempPath.Create();
        }

        try
        {
            var serviceCollection = new ServiceCollection();
            var collection = serviceCollection
                .AddSingleton<IViewModelFactory, ViewModelFactory>()
                .AddSingleton<MainWindowViewModel>()
                .AddSingleton<MainWindow>()
                .AddTransient<AutoMapModelTypeConverter>()
                .AddSingleton<GlobalOptions>((_) => GlobalOptions.LoadOrCreate().Result)
                .AddSingleton<IPromptsResource, PromptsResourceViewModel>()
                .AddSingleton<IEndpointService, EndpointConfigureViewModel>()
                .AddSingleton<IRagSourceCollection, RagSourceCollection>()
                .AddTransient<NvidiaResearchClientOption>()
                .AddSingleton<IMcpServiceCollection, McpServiceCollection>()
                .AddSingleton<IBuiltInFunctionsCollection, BuiltInFunctionsCollection>()
                .AddSingleton<CreateSessionViewModel>()
                .AddSingleton<ITokensCounter, DefaultTokensCounter>()
                .AddSingleton<DialogMappingProfile>()
                .AddMap();
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
                builder.AddDebug();
                // Add OpenTelemetry as a logging provider
                builder.AddOpenTelemetry(options =>
                {
                    options.SetResourceBuilder(resourceBuilder);
                    options.AddConsoleExporter();
                    // Format log messages. This is default to false.
                    options.IncludeFormattedMessage = true;
                    options.IncludeScopes = true;
                });
                builder.SetMinimumLevel(LogLevel.Trace);
            });
#else
            var logPath = Path.GetFullPath("Logs");
            if (!Directory.Exists(logPath))
            {
                Directory.CreateDirectory(logPath);
            }

            collection.AddLogging(builder =>
            {
                builder.SetMinimumLevel(LogLevel.Information);
            });
#endif
            serviceProvider = collection.BuildServiceProvider();
            BaseViewModel.ServiceLocator = serviceProvider;
#if DEBUG
            //禁止在Debug模式下注册LoggerTraceListener，避免重复日志
#else
            //注册LoggerFactory到Listeners
            var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
            var traceListener = new LLMClient.Log.LoggerTraceListener(loggerFactory);
            Trace.Listeners.Add(traceListener);
#endif
            ContextExtension.RegisterMSBuild();
            App app = new App();
            app.InitializeComponent();
            //app.Run(new AsyncTestWindow());
            mainWindow = serviceProvider.GetService<MainWindow>();
            app.Run(mainWindow);
        }
        catch (Exception e)
        {
            MessageBox.Show("An error occured: " + e + "process will be terminated.");
            var logger = serviceProvider?.GetService<ILogger<Program>>();
            logger?.LogCritical(e, "Application terminated unexpectedly");
            try
            {
                if (mainWindow?.DataContext is MainWindowViewModel { IsInitialized: true } mainWindowViewModel)
                {
                    mainWindowViewModel.SaveDataAsync().Wait(TimeSpan.FromSeconds(30));
                }
            }
            catch (Exception exception)
            {
                logger?.LogError(exception, "保存数据失败");
            }
        }
        finally
        {
            if (!Debugger.IsAttached)
            {
                Mutex.ReleaseMutex();
            }

            // tempPath.Delete(true);//默认不删除}
        }
    }
}