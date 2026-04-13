using LLMClient.Abstraction;

namespace LLMClient.Dialog.Proc;

public sealed class NoOpChatHistoryCompressionStrategy : IChatHistoryCompressionStrategy
{
    public Task CompressAsync(ChatHistoryCompressionContext context, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
