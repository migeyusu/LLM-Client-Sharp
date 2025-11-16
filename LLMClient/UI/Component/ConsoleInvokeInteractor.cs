using LLMClient.Endpoints;

namespace LLMClient.UI.Component;

public class ConsoleInvokeInteractor : IInvokeInteractor
{
    public void Info(string message)
    {
        Console.Write(message);
    }

    public void Error(string message)
    {
        Console.WriteLine(message);
    }

    public void Warning(string message)
    {
        Console.WriteLine(message);
    }

    public void WriteLine(string? message = null)
    {
        Console.WriteLine(message);
    }

    public bool WaitForPermission(string message)
    {
        Console.WriteLine(message);
        Console.WriteLine("Press Enter to continue...");
        Console.ReadLine();
        return true;
    }

    public bool WaitForPermission(object content)
    {
        Console.WriteLine(content);
        Console.WriteLine("Press Enter to continue...");
        Console.ReadLine();
        return true;
    }
}