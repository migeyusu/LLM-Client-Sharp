using System.Diagnostics;

namespace LLMClient.Log;

public class LogItem
{
    public TraceEventType Type { get; set; }

    public string? Message { get; set; }

    public int Id { get; set; }

    public string? Source { get; set; }

    public DateTime Time { get; } = DateTime.Now;

    public override string ToString()
    {
        string time = DateTime.Now.ToString("HH:mm:ss.fff");
        return $"[{time}] [{Type}] {Message}";
    }
}