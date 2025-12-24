using Microsoft.Extensions.AI;

namespace LLMClient.Endpoints;

public class LLMBadRequestException : Exception
{
    public ChatResponse? ChatResponse { get; set; }

    public LLMBadRequestException() : base("The request was invalid or cannot be served by the LLM endpoint.")
    {
    }

    public LLMBadRequestException(string message) : base(message)
    {
    }
}

/// <summary>
/// 指示过程超过上下文
/// </summary>
public class OutOfContextWindowException : LLMBadRequestException
{
    public ChatResponse? ChatResponse { get; set; }

    public OutOfContextWindowException() : base("Exceeded maximum response length.")
    {
    }
}