using System.Diagnostics;
using System.Text;
using System.Windows;
using AutoMapper;
using LLMClient.Abstraction;
using LLMClient.Component;
using LLMClient.Component.ViewModel;
using LLMClient.Component.ViewModel.Base;
using LLMClient.Configuration;
using LLMClient.ContextEngineering.Analysis;
using LLMClient.Dialog;
using LLMClient.Endpoints;
using LLMClient.Log;
using LLMClient.Persistence;
using LLMClient.Rag;
using LLMClient.ToolCall.DefaultPlugins;
using LLMClient.ToolCall.MCP;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Resources;

namespace LLMClient;

public class Program
{
    private static readonly Mutex Mutex = new Mutex(true, "LLMClient.WPF");
    static IServiceProvider? _serviceProvider = null;
    [STAThread]
    static void Main()
    {
        // 尝试获得互斥量的所有权
        if (!Debugger.IsAttached && !Mutex.WaitOne(TimeSpan.Zero, true))
        {
            // 如果获取失败，则表示已有实例在运行
            MessageBox.Show("程序已经在运行！", "提示");
            return;
        }

        
        MainWindow? mainWindow = null;
        DailyRollingLogSink? logSink = null;
        DailyRollingTraceListener? traceFileListener = null;
        LoggerTraceListener? loggerTraceListener = null;
        CrashGuard? crashGuard = null;
        var tempPath = new DirectoryInfo(Extension.TempPath);
        if (!tempPath.Exists)
        {
            tempPath.Create();
        }

        try
        {
            var logPath = Path.GetFullPath("Logs");
            if (!Directory.Exists(logPath))
            {
                Directory.CreateDirectory(logPath);
            }

            logSink = new DailyRollingLogSink(logPath);
            traceFileListener = new DailyRollingTraceListener(logSink);
            Trace.Listeners.Add(traceFileListener);
            Trace.AutoFlush = true;
            crashGuard = new CrashGuard(logPath, () => _serviceProvider?.GetService<ILogger<Program>>());
            crashGuard.RegisterProcessHandlers();
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            var serviceCollection = new ServiceCollection();
            var collection = serviceCollection
                .AddSingleton<IViewModelFactory, ViewModelFactory>()
                .AddSingleton<MainWindowViewModel>()
                .AddSingleton<MainWindow>()
                .AddTransient<AutoMapModelTypeConverter>()
                .AddSingleton<GlobalOptions>((_) => GlobalOptions.LoadOrCreate().Result)
                .AddSingleton<IPromptsResource, PromptsResourceViewModel>()
                .AddSingleton<IEndpointService, EndpointConfigureViewModel>()
                .AddSingleton<NewApiUsageQueryService>()
                .AddSingleton<IRagSourceCollection, RagSourceCollection>()
                .AddSingleton<IMcpServiceCollection, McpServiceCollection>()
                .AddSingleton<BuiltInFunctionsCollection>()
                .AddSingleton<CreateSessionViewModel>()
                .AddSingleton<ITokensCounter, DefaultTokensCounter>()
                .AddSingleton<ChatHistoryCompressionStrategyFactory>()
                .AddSingleton<Profile, FunctionGroupPersistenceProfile>()
                .AddSingleton<Profile, DialogItemPersistenceProfile>()
                .AddSingleton<Profile, DialogMappingProfile>()
                .AddSingleton<Profile, SessionProjectPersistenceProfile>()
                .AddSingleton<Profile, RoslynMappingProfile>()
                .AddTransient<RoslynProjectAnalyzer>()
                .AddTransient<AnalyzerConfig>()
                .AddSingleton<Summarizer>()
                .AddMap();
#if DEBUG
            collection.AddLogging(builder =>
            {
                builder.AddDebug();
                builder.AddProvider(new DailyRollingFileLoggerProvider(logSink));
                // Add OpenTelemetry as a logging provider
                builder.AddOpenTelemetry(options =>
                {
                    options.IncludeFormattedMessage = true;
                    options.IncludeScopes = true;
                });
                builder.SetMinimumLevel(LogLevel.Trace);
            });
#else
            collection.AddLogging(builder =>
            {
                builder.AddProvider(new DailyRollingFileLoggerProvider(logSink));
                builder.SetMinimumLevel(LogLevel.Information);
            });
#endif
            _serviceProvider = collection.BuildServiceProvider();
            BaseViewModel.ServiceLocator = _serviceProvider;
#if RELEASE
        // Release 下通过 ILogger 管道统一写文件，避免 Trace 直写和 ILogger 文件 provider 重复落盘。
        Trace.Listeners.Remove(traceFileListener);
        traceFileListener.Dispose();
        traceFileListener = null;
        var loggerFactory = _serviceProvider.GetRequiredService<ILoggerFactory>();
        loggerTraceListener = new LoggerTraceListener(loggerFactory);
        Trace.Listeners.Add(loggerTraceListener);
#endif
            AnalyzerExtension.RegisterMsBuild();
            App app = new App();
            app.InitializeComponent();
            crashGuard.AttachApplication(app);
            // app.Run(new AsyncTestWindow());
            mainWindow = _serviceProvider.GetService<MainWindow>();
            app.Run(mainWindow);
        }
        catch (Exception e)
        {
            MessageBox.Show("An error occured: " + e + "process will be terminated.", "Error", MessageBoxButton.OK,
                MessageBoxImage.Error);
            var logger = _serviceProvider?.GetService<ILogger<Program>>();
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
            crashGuard?.Dispose();

            try
            {
                Trace.Flush();
            }
            catch
            {
                // 忽略退出阶段的日志刷新异常，避免影响应用退出。
            }

            if (loggerTraceListener is not null)
            {
                Trace.Listeners.Remove(loggerTraceListener);
                loggerTraceListener.Dispose();
            }

            if (traceFileListener is not null)
            {
                Trace.Listeners.Remove(traceFileListener);
                traceFileListener.Dispose();
            }

            logSink?.Dispose();

            if (!Debugger.IsAttached)
            {
                Mutex.ReleaseMutex();
            }

            // tempPath.Delete(true);//默认不删除}
        }
    }
}