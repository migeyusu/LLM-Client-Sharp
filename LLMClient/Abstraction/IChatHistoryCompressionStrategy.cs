
namespace LLMClient.Abstraction;

public interface IChatHistoryCompressionStrategy
{
    Task CompressAsync(ChatHistoryContext context, CancellationToken cancellationToken = default);
    
}

