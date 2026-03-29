using System.Diagnostics;
using LLMClient.Log;

namespace LLMClient.Test;

public sealed class DailyRollingTraceListenerTests : IDisposable
{
    private readonly string _tempDirectory = Path.Combine(Path.GetTempPath(), "LLMClient.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void WritesTraceMessagesToDateBasedLogFile()
    {
        var now = new DateTimeOffset(2026, 3, 29, 8, 30, 15, TimeSpan.FromHours(8));

        using (var listener = new DailyRollingTraceListener(_tempDirectory, nowProvider: () => now))
        {
            listener.WriteLine("plain trace message");
            listener.TraceEvent(null, "UnitTest", TraceEventType.Warning, 7, "warning message");
            listener.Flush();
        }

        var logFile = Path.Combine(_tempDirectory, "trace-2026-03-29.log");
        Assert.True(File.Exists(logFile));

        var content = File.ReadAllText(logFile);
        Assert.Contains("[Information] plain trace message", content);
        Assert.Contains("[Warning] [UnitTest] [EventId:7] warning message", content);
    }

    [Fact]
    public void RollsOverWhenDateChanges()
    {
        var clock = new TestClock(new DateTimeOffset(2026, 3, 29, 23, 59, 58, TimeSpan.Zero));

        using (var listener = new DailyRollingTraceListener(_tempDirectory, nowProvider: () => clock.Current))
        {
            listener.WriteLine("day one");
            clock.Current = clock.Current.AddSeconds(5);
            listener.WriteLine("day two");
            listener.Flush();
        }

        var firstLogFile = Path.Combine(_tempDirectory, "trace-2026-03-29.log");
        var secondLogFile = Path.Combine(_tempDirectory, "trace-2026-03-30.log");

        Assert.True(File.Exists(firstLogFile));
        Assert.True(File.Exists(secondLogFile));
        Assert.Contains("day one", File.ReadAllText(firstLogFile));
        Assert.Contains("day two", File.ReadAllText(secondLogFile));
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    private sealed class TestClock(DateTimeOffset current)
    {
        public DateTimeOffset Current { get; set; } = current;
    }
}
