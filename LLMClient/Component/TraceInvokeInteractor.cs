using System.Diagnostics;
using LLMClient.Endpoints;

namespace LLMClient.Component;

public class TraceInvokeInteractor : IInvokeInteractor
{
    public void Info(string message)
    {
        Trace.Write(message);
    }

    public void Error(string message)
    {
        Trace.WriteLine(message);
    }

    public void Warning(string message)
    {
        Trace.WriteLine(message);
    }

    public void Write(string message)
    {
        Trace.Write(message);
    }

    public void WriteLine(string? message = null)
    {
        Trace.WriteLine(message);
    }

    public Task<bool> WaitForPermission(string title, string message)
    {
        Trace.WriteLine(title);
        Trace.WriteLine(message);
        Trace.WriteLine("Permission requested, automatically granted in TraceInvokeInteractor.");
        return Task.FromResult(true);
    }

    public Task<bool> WaitForPermission(object content)
    {
        Trace.WriteLine(content);
        Trace.WriteLine("Permission requested, automatically granted in TraceInvokeInteractor.");
        return Task.FromResult(true);
    }
}