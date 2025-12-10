using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Threading;

namespace LLMClient.Component.Utility
{
    /// <summary>
    /// 提供一个可等待的对象，用于将执行上下文切换到UI线程。
    /// 用法: await UIThread.SwitchTo();
    /// </summary>
    public static class UIThread
    {
        /// <summary>
        /// 返回一个可等待的对象，当被等待时，它会确保后续代码在UI线程上执行。
        /// </summary>
        public static UIThreadAwaitable SwitchTo()
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null)
            {
                throw new InvalidOperationException("无法访问 Dispatcher. 确保 Application 对象存在。");
            }
            return new UIThreadAwaitable(dispatcher);
        }
    }
    
    public readonly struct UIThreadAwaitable
    {
        private readonly Dispatcher _dispatcher;

        public UIThreadAwaitable(Dispatcher dispatcher)
        {
            _dispatcher = dispatcher;
        }
        
        public UIThreadAwaiter GetAwaiter() => new UIThreadAwaiter(_dispatcher);
    }
    
    public readonly struct UIThreadAwaiter : INotifyCompletion, ICriticalNotifyCompletion
    {
        private readonly Dispatcher _dispatcher;

        public UIThreadAwaiter(Dispatcher dispatcher)
        {
            _dispatcher = dispatcher;
        }
        
        public bool IsCompleted => _dispatcher.CheckAccess();
        
        public void OnCompleted(Action continuation)
        {
            _dispatcher.BeginInvoke(continuation, DispatcherPriority.Normal);
        }
        
        public void UnsafeOnCompleted(Action continuation)
        {
            _dispatcher.BeginInvoke(continuation, DispatcherPriority.Normal);
        }

        public void GetResult() { }
    }
}