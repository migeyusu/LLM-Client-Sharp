using Microsoft.Extensions.AI;

namespace LLMClient.Endpoints;

public class ChatCriticalException : Exception
{
    public ChatCriticalException(string message) : base(message)
    {
    }

    public ChatCriticalException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

public class LlmInvalidRequestException : ChatCriticalException
{
    public LlmInvalidRequestException() : base("The request was invalid or cannot be served by the LLM endpoint.")
    {
    }

    public LlmInvalidRequestException(string message) : base(message)
    {
    }

    public LlmInvalidRequestException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

/// <summary>
/// 指示过程超过上下文
/// </summary>
public class OutOfContextWindowException : LlmInvalidRequestException
{
    public ChatResponse? ChatResponse { get; }

    public OutOfContextWindowException(ChatResponse chatResponse)
        : base("Exceeded maximum response length." + chatResponse.RawRepresentation)
    {
        this.ChatResponse = chatResponse;
    }

    public OutOfContextWindowException(Exception innerException)
        : base("Exceeded maximum response length.", innerException)
    {
    }
}

public class ResultFilteredException : LlmInvalidRequestException
{
}