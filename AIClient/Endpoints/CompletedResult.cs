using LLMClient.UI;
using Microsoft.Extensions.AI;

namespace LLMClient.Endpoints;

public class CompletedResult : IResponse
{
    public static readonly CompletedResult Empty = new CompletedResult(null, new UsageDetails(), null);

    public CompletedResult(string? response, UsageDetails usage, double? price)
    {
        Raw = response;
        Usage = usage;
        Price = price;
    }

    public string? Raw { get; set; }

    public UsageDetails Usage { get; set; }

    public long Tokens
    {
        get { return Usage.OutputTokenCount ?? 0; }
    }

    public int Latency { get; set; }

    public int Duration { get; set; }

    public string? ErrorMessage { get; set; }

    public double? Price { get; }

    public bool IsInterrupt
    {
        get { return ErrorMessage != null; }
    }
}