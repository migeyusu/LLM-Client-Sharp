namespace LLMClient.Abstraction;

public sealed class NoOpChatHistoryCompressionStrategy : IChatHistoryCompressionStrategy
{
    public static NoOpChatHistoryCompressionStrategy Instance { get; } = new();

    private NoOpChatHistoryCompressionStrategy()
    {
    }

    public Task CompressAsync(ChatHistoryCompressionContext context, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}

