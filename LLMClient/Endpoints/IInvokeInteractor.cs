namespace LLMClient.Endpoints;

public interface IInvokeInteractor
{
    void Info(string message);

    void Error(string message);

    void Warning(string message);

    void Write(string message);

    void WriteLine(string? message = null);

    Task<bool> WaitForPermission(string title, string message);

    Task<bool> WaitForPermission(object content);
}