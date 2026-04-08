using Microsoft.Extensions.AI;

namespace LLMClient.Abstraction;

public interface IChatHistoryCompressionStrategy
{
    bool ShouldCompress(ChatHistoryCompressionContext context);

    Task CompressAsync(ChatHistoryCompressionContext context, CancellationToken cancellationToken = default);
}
