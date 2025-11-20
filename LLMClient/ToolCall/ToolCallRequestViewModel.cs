namespace LLMClient.ToolCall;


public class ToolCallRequestViewModel
{
    public string? CallerClassName { get; set; }

    public string? CallerMethodName { get; set; }

    public required string Message { get; set; }
}