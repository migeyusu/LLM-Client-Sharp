using Microsoft.Extensions.AI;

namespace LLMClient.Endpoints;

public class CompletedResult
{
    public static CompletedResult Empty = new CompletedResult(null, new UsageDetails());

    public CompletedResult(string? response, UsageDetails usage)
    {
        Response = response;
        Usage = usage;
    }

    public string? Response { get; set; }

    public UsageDetails Usage { get; set; }

    public string? ErrorMessage { get; set; }

    public bool IsInterrupt
    {
        get { return ErrorMessage != null; }
    }
}