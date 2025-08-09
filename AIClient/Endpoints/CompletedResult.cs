using LLMClient.Abstraction;
using LLMClient.Endpoints.Messages;
using Microsoft.Extensions.AI;
using ChatFinishReason = Microsoft.Extensions.AI.ChatFinishReason;
using ChatMessage = Microsoft.Extensions.AI.ChatMessage;

namespace LLMClient.Endpoints;

public class CompletedResult : IResponse
{
    public static readonly CompletedResult Empty =
        new CompletedResult();

    public UsageDetails? Usage { get; set; }

    public long Tokens
    {
        get { return Usage?.OutputTokenCount ?? 0; }
    }

    public int Latency { get; set; }

    public int Duration { get; set; }

    public string? ErrorMessage { get; set; }

    public double? Price { get; set; }

    public bool IsInterrupt
    {
        get { return ErrorMessage != null; }
    }

    public ChatFinishReason? FinishReason { get; set; }

    public string? TextResponse
    {
        get { return ResponseMessages?.FirstOrDefault()?.Text; }
    }
    
    public IList<ChatMessage>? ResponseMessages { get; set; }

    public IList<ChatAnnotation>? Annotations { get; set; }

    public AdditionalPropertiesDictionary? AdditionalProperties { get; set; }
}