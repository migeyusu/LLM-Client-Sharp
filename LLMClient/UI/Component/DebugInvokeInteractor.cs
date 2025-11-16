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
    
    public void WriteLine(string? message = null)
    {
        Debug.WriteLine(message);
    }

    public bool WaitForPermission(string message)
    {
        return true;
    }

    public bool WaitForPermission(object content)
    {
        return true;
    }
}