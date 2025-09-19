using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Threading;

namespace LLMClient.UI // 放在您的公共帮助类库中
{
    /// <summary>
    /// 为 Task 提供扩展方法，以允许在等待后在指定的 Dispatcher 上恢复执行。
    /// </summary>
    public static class TaskResumeOnExtensions
    {
        /// <summary>
        /// 等待任务完成，并确保后续代码在当前应用程序的UI线程上执行。
        /// </summary>
        /// <param name="task">要等待的任务。</param>
        public static ResumeOnAwaitable ResumeOnUI(this Task task)
        {
            var dispatcher = Application.Current?.Dispatcher 
                             ?? throw new InvalidOperationException("无法获取当前 Application 的 Dispatcher。");
            return new ResumeOnAwaitable(task, dispatcher);
        }

        /// <summary>
        /// 等待任务完成，并确保后续代码在当前应用程序的UI线程上执行。
        /// </summary>
        /// <typeparam name="T">任务的结果类型。</typeparam>
        /// <param name="task">要等待的任务。</param>
        public static ResumeOnAwaitable<T> ResumeOnUI<T>(this Task<T> task)
        {
            var dispatcher = Application.Current?.Dispatcher 
                             ?? throw new InvalidOperationException("无法获取当前 Application 的 Dispatcher。");
            return new ResumeOnAwaitable<T>(task, dispatcher);
        }

        /// <summary>
        /// 等待任务完成，并确保后续代码在指定的 Dispatcher 上恢复执行。
        /// </summary>
        /// <param name="task">要等待的任务。</param>
        /// <param name="dispatcher">后续代码将在此 Dispatcher 上执行。</param>
        public static ResumeOnAwaitable ResumeOn(this Task task, Dispatcher dispatcher)
        {
            return new ResumeOnAwaitable(task, dispatcher);
        }

        /// <summary>
        /// 等待任务完成，并确保后续代码在指定的 Dispatcher 上恢复执行。
        /// </summary>
        /// <typeparam name="T">任务的结果类型。</typeparam>
        /// <param name="task">要等待的任务。</param>
        /// <param name="dispatcher">后续代码将在此 Dispatcher 上执行。</param>
        public static ResumeOnAwaitable<T> ResumeOn<T>(this Task<T> task, Dispatcher dispatcher)
        {
            return new ResumeOnAwaitable<T>(task, dispatcher);
        }
    }

    /// <summary>
    /// 可等待的结构体，用于包装 Task 和目标 Dispatcher。
    /// </summary>
    public readonly struct ResumeOnAwaitable
    {
        private readonly Task _task;
        private readonly Dispatcher _dispatcher;

        public ResumeOnAwaitable(Task task, Dispatcher dispatcher)
        {
            _task = task;
            _dispatcher = dispatcher;
        }

        public ResumeOnAwaiter GetAwaiter() => new ResumeOnAwaiter(_task, _dispatcher);
    }
    
    /// <summary>
    /// 可等待的结构体（泛型版本），用于包装Task^T 和目标 Dispatcher。
    /// </summary>
    public readonly struct ResumeOnAwaitable<T>
    {
        private readonly Task<T> _task;
        private readonly Dispatcher _dispatcher;

        public ResumeOnAwaitable(Task<T> task, Dispatcher dispatcher)
        {
            _task = task;
            _dispatcher = dispatcher;
        }

        public ResumeOnAwaiter<T> GetAwaiter() => new ResumeOnAwaiter<T>(_task, _dispatcher);
    }

    /// <summary>
    /// Awaiter实现，它等待一个Task，然后将 continuation 封送到指定的 Dispatcher。
    /// </summary>
    public readonly struct ResumeOnAwaiter : INotifyCompletion
    {
        private readonly TaskAwaiter _taskAwaiter;
        private readonly Dispatcher _dispatcher;

        public ResumeOnAwaiter(Task task, Dispatcher dispatcher)
        {
            _taskAwaiter = task.GetAwaiter();
            _dispatcher = dispatcher;
        }

        // IsCompleted 直接代理到内部任务的 Awaiter。
        // 这意味着如果任务已经完成，我们可以尝试同步继续。
        public bool IsCompleted => _taskAwaiter.IsCompleted;

        public void OnCompleted(Action continuation)
        {
            // 这是核心：我们为原始任务注册一个 continuation。
            // 当原始任务完成时（可能在任何线程上），这个 lambda 会被调用。
            var dispatcher = _dispatcher;
            _taskAwaiter.OnCompleted(() =>
            {
                // 在这个 lambda 内部，我们不直接执行 'continuation'，
                // 而是把它作为一个新的工作项，发布到我们想要的目标 Dispatcher 上。
                dispatcher.BeginInvoke(continuation);
            });
        }
        
        // GetResult 也代理到内部任务的 Awaiter。
        // 这会获取任务的结果，或者如果任务失败，则重新抛出异常。
        public void GetResult() => _taskAwaiter.GetResult();
    }
    
    /// <summary>
    /// Awaiter实现（泛型版本）。
    /// </summary>
    public readonly struct ResumeOnAwaiter<T> : INotifyCompletion
    {
        private readonly TaskAwaiter<T> _taskAwaiter;
        private readonly Dispatcher _dispatcher;

        public ResumeOnAwaiter(Task<T> task, Dispatcher dispatcher)
        {
            _taskAwaiter = task.GetAwaiter();
            _dispatcher = dispatcher;
        }

        public bool IsCompleted => _taskAwaiter.IsCompleted;

        public void OnCompleted(Action continuation)
        {
            var dispatcher = _dispatcher;
            _taskAwaiter.OnCompleted(() =>
            {
                dispatcher.BeginInvoke(continuation);
            });
        }
        
        public T GetResult() => _taskAwaiter.GetResult();
    }
}