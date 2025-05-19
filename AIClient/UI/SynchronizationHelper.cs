namespace LLMClient.UI;

/* SynchronizationContext是一个比dispatcher更通用的后台线程调用UI对象，
 * begininvoke：通过向一个队列发送带有优先级的delegate，被sta thread调用实现UI刷新（异步）
 * invoke：阻塞sta thread直接强迫执行（同步）
 * 在wpf下获取SynchronizationContext.Current得到的是DispatcherSynchronizationContext
 * 在winform下获取得到的是WindowsFormsSynchronizationContext，所以更通用
 */

/// <summary>
/// 框架跨线程操作基础，包括集合类
/// </summary>
public static class SynchronizationHelper
{
    private static SynchronizationContext? _uiContext;

    /// <summary>
    /// 必须在STA主线程下初始化
    /// </summary>
    public static void Initialize()
    {
        if (_uiContext != null)
            return;
        if (SynchronizationContext.Current == null)
        {
            SynchronizationContext.SetSynchronizationContext(new SynchronizationContext());
        }

        _uiContext = SynchronizationContext.Current;
    }

    public static void Inovke(Action<object> action, object state)
    {
        if (_uiContext == null)
        {
            throw new NullReferenceException("未初始化同步帮助类");
        }

        _uiContext.Send(o => action(o), state);
    }

    public static void InvokeAsync(Action<object> action, object state)
    {
        if (_uiContext == null)
        {
            throw new NullReferenceException("未初始化同步帮助类");
        }

        _uiContext.Post(o => action(o), state);
    }
}