using System.Windows;
using System.Windows.Threading;

namespace LLMClient.UI.Component
{
    /* SynchronizationContext是一个比dispatcher更通用的后台线程调用UI对象，
     * begininvoke：通过向一个队列发送带有优先级的delegate，被sta thread调用实现UI刷新（异步）
     * invoke：阻塞sta thread直接强迫执行（同步）
     * 在wpf下获取SynchronizationContext.Current得到的是DispatcherSynchronizationContext
     * 在winform下获取得到的是WindowsFormsSynchronizationContext*/


    /// <summary>
    /// 框架跨线程操作基础，包括集合类
    /// </summary>
    public static class DispatcherEx
    {
        public static PostDispatcherrAwaiter GetAwaiter(this Dispatcher context)
        {
            return new PostDispatcherrAwaiter(context);
        }

        public static System.Windows.Threading.Dispatcher Dispatcher()
        {
            return Application.Current.Dispatcher;
        }

        public static Task SwitchToUIThread()
        {
            var dispatcher = Application.Current.Dispatcher;
            if (dispatcher.CheckAccess())
                return Task.CompletedTask;

            var tcs = new TaskCompletionSource<bool>();
            dispatcher.BeginInvoke(() => tcs.SetResult(true));
            return tcs.Task;
        }
    }
}