using Microsoft.Extensions.AI;

namespace LLMClient.Endpoints;

public class LlmBadRequestException : Exception
{
    public LlmBadRequestException() : base("The request was invalid or cannot be served by the LLM endpoint.")
    {
    }

    public LlmBadRequestException(string message) : base(message)
    {
    }
}

/// <summary>
/// 指示过程超过上下文
/// </summary>
public class OutOfContextWindowException : LlmBadRequestException
{
    public ChatResponse? ChatResponse { get; set; }

    public OutOfContextWindowException() : base("Exceeded maximum response length.")
    {
    }
}