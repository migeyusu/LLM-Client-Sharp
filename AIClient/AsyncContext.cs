

// TInstance 可以是您希望在上下文中传递的任何类型
namespace LLMClient;

public static class AsyncContext<TInstance> where TInstance : class
{
    // 内部类，用于持有上下文实例和指向父级上下文的链接
    private sealed class ChatContextHolder
    {
        public TInstance? Instance { get; set; }
        public ChatContextHolder? Parent { get; set; }
    }

    // 核心：使用AsyncLocal来存储上下文持有者
    private static readonly AsyncLocal<ChatContextHolder> AsyncLocalContext = new AsyncLocal<ChatContextHolder>();

    /// <summary>
    /// 获取当前异步上下文中存储的实例。
    /// 如果不在ChatContext的作用域内，则返回null。
    /// </summary>
    public static TInstance? Current => AsyncLocalContext.Value?.Instance;

    /// <summary>
    /// 创建一个新的上下文作用域。
    /// 在此作用域内以及其所有异步/同步子调用中，可以通过 ChatContext.Current 获取到传入的实例。
    /// </summary>
    /// <param name="instance">要设置在此上下文作用域中的实例。</param>
    /// <returns>一个IDisposable对象，该对象在被释放时会自动恢复到上一个上下文。</returns>
    public static IDisposable Create(TInstance instance)
    {
        if (instance == null) throw new ArgumentNullException(nameof(instance));

        var currentHolder = AsyncLocalContext.Value;
        // 创建新的上下文持有者，并将其Parent链接到当前的持有者（支持嵌套）
        AsyncLocalContext.Value = new ChatContextHolder { Instance = instance, Parent = currentHolder };
        // 返回一个可释放的对象，其Dispose方法将用于恢复上下文
        return new ContextRestorer(currentHolder);
    }

    // 一个私有的辅助类，实现了IDisposable接口，用于在using结束后恢复上下文
    private sealed class ContextRestorer : IDisposable
    {
        private readonly ChatContextHolder? _holderToRestore;
        private bool _disposed;

        public ContextRestorer(ChatContextHolder? holderToRestore)
        {
            _holderToRestore = holderToRestore;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                if (_holderToRestore != null)
                {
                }

                // 将AsyncLocal的值恢复为进入using之前的状态
                AsyncLocalContext.Value = _holderToRestore;
                _disposed = true;
            }
        }
    }
}