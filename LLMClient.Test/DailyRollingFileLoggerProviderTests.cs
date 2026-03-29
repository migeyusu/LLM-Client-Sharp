using System.Diagnostics;
using LLMClient.Log;
using Microsoft.Extensions.Logging;

namespace LLMClient.Test;

public sealed class DailyRollingFileLoggerProviderTests : IDisposable
{
    private readonly string _tempDirectory = Path.Combine(Path.GetTempPath(), "LLMClient.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void WritesILoggerMessagesToDateBasedLogFile()
    {
        var now = new DateTimeOffset(2026, 3, 29, 9, 15, 0, TimeSpan.FromHours(8));

        using (var sink = new DailyRollingLogSink(_tempDirectory, nowProvider: () => now))
        {
            using var provider = new DailyRollingFileLoggerProvider(sink);
            var logger = provider.CreateLogger("Tests.Logger");
            logger.LogError(new InvalidOperationException("boom"), "logger message {Value}", 42);
            sink.Flush();
        }

        var logFile = Path.Combine(_tempDirectory, "trace-2026-03-29.log");
        Assert.True(File.Exists(logFile));

        var content = File.ReadAllText(logFile);
        Assert.Contains("[Error] [Tests.Logger] logger message 42", content);
        Assert.Contains("InvalidOperationException: boom", content);
    }

    [Fact]
    public void TraceAndILoggerShareTheSameLogFile()
    {
        var now = new DateTimeOffset(2026, 3, 29, 10, 0, 0, TimeSpan.FromHours(8));

        using (var sink = new DailyRollingLogSink(_tempDirectory, nowProvider: () => now))
        {
            using var traceListener = new DailyRollingTraceListener(sink);
            using var provider = new DailyRollingFileLoggerProvider(sink);

            traceListener.TraceEvent(null, "TraceSource", TraceEventType.Warning, 3, "trace message");
            provider.CreateLogger("Tests.Logger").LogInformation("logger message");
            sink.Flush();
        }

        var logFile = Path.Combine(_tempDirectory, "trace-2026-03-29.log");
        Assert.True(File.Exists(logFile));

        var content = File.ReadAllText(logFile);
        Assert.Contains("[Warning] [TraceSource] [EventId:3] trace message", content);
        Assert.Contains("[Information] [Tests.Logger] logger message", content);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }
}
