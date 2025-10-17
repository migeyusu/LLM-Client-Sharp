using LLMClient.Abstraction;
using LLMClient.Dialog;
using LLMClient.Endpoints;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace LLMScripts;

class Program
{
    static void Main(string[] args)
    {
        var collection = new ServiceCollection();
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
            builder.SetMinimumLevel(LogLevel.Information);
        });

        ILLMChatClient chatClient;
    }
}

class Processor
{
    public static async Task GetResponse(ILLMChatClient chatClient, string request,
        SemaphoreSlim clientSemaphore, ILogger? logger = null, int retryCount = 3)
    {
        var modelParams = chatClient.Parameters;
        modelParams.Streaming = false;
        var dialogContext = new DialogContext([
            new RequestViewItem() { TextMessage = request, }
        ]);
        var response = new CompletedResult();
        int tryCount = 0;
        while (tryCount < retryCount)
        {
            response = await client.SendRequest(dialogContext, cancellationToken: token);
            tryCount++;
            var textResponse = response.FirstTextResponse;
            if (!string.IsNullOrEmpty(textResponse) && !response.IsInterrupt)
            {
                cache.TryAdd(raw, textResponse);
                return textResponse;
            }
        }
    }
}

/// <summary>
/// 用于对TOC执行注释
/// </summary>
class TOCANADescriptor
{
    private const string root = @"E:\Dev\toc_multisimult_design\00 源码\TOCAnalyzer";

    public async Task Start()
    {
    }
}