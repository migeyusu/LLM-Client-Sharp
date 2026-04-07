using Microsoft.Extensions.AI;

namespace LLMClient.Abstraction;

public interface IChatHistoryCompressionStrategy
{
    Task CompressAsync(ChatHistoryCompressionContext context, CancellationToken cancellationToken = default);
}

