using DocumentFormat.OpenXml.Office2013.Excel;
using LLMClient.Abstraction;
using LLMClient.Endpoints.Messages;
using LLMClient.UI;
using LLMClient.UI.Dialog;
using Microsoft.Extensions.AI;
using OpenAI.Chat;
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

    public IList<ChatMessage>? ResponseMessages { get; set; }

    public IList<ChatAnnotation>? Annotations { get; set; }

    public AdditionalPropertiesDictionary? AdditionalProperties { get; set; }
}