using Microsoft.Extensions.AI;

namespace LLMClient.Abstraction;

public interface IChatUsage
{
    double? Price { get; }

    UsageDetails? Usage { get; }
}