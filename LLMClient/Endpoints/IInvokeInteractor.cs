namespace LLMClient.Endpoints;

public interface IInvokeInteractor
{
    void Info(string message);

    void Error(string message);

    void Warning(string message);

    void WriteLine(string? message = null);

    bool WaitForPermission(string message);

    bool WaitForPermission(object content);
}