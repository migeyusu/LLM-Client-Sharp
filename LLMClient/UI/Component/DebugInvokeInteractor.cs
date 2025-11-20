using System.Diagnostics;
using LLMClient.Endpoints;

namespace LLMClient.UI.Component;

public class DebugInvokeInteractor : IInvokeInteractor
{
    public void Info(string message)
    {
        Debug.Write(message);
    }

    public void Error(string message)
    {
        Debug.WriteLine(message);
    }

    public void Warning(string message)
    {
        Debug.WriteLine(message);
    }

    public void Write(string message)
    {
        Debug.Write(message);
    }

    public void WriteLine(string? message = null)
    {
        Debug.WriteLine(message);
    }

    public Task<bool> WaitForPermission(string message)
    {
        return Task.FromResult(true);
    }

    public Task<bool> WaitForPermission(object content)
    {
        return Task.FromResult(true);
    }
}