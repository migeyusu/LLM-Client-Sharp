using Microsoft.Extensions.AI;

namespace LLMClient.Endpoints;

/// <summary>
/// 指示过程超过上下文
/// </summary>
public class OutOfContextWindowException : Exception
{
    public ChatResponse? ChatResponse { get; set; }

    public OutOfContextWindowException() : base("Exceeded maximum response length.")
    {
    }
}