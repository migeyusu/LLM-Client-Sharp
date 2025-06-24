using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Threading;

namespace LLMClient.UI.Log;

/// <summary>
/// 把 Trace 写入 ObservableCollection 的监听器
/// </summary>
public sealed class ObservableCollectionTraceListener : TraceListener
{
    private readonly ObservableCollection<LogItem> _target;
    private readonly Dispatcher _dispatcher;

    public ObservableCollectionTraceListener(ObservableCollection<LogItem> target,
        Dispatcher? uiDispatcher = null)
    {
        _target = target;
        _dispatcher = uiDispatcher ?? Dispatcher.CurrentDispatcher;
    }

    public override void Write(string? message) => Append(new LogItem()
        { Id = -1, Type = TraceEventType.Information, Message = message });

    public override void WriteLine(string? message) =>
        Append(new LogItem() { Id = -1, Type = TraceEventType.Information, Message = message }, appendLine: true);

    // 如果你想要更丰富的分类/时间戳信息，可以再重写 TraceEvent
    public override void TraceEvent(TraceEventCache? eventCache, string source,
        TraceEventType eventType, int id, string? message)
    {
        Append(new LogItem()
        {
            Message = message,
            Type = eventType,
            Source = source,
            Id = id,
        });
    }

    private void Append(LogItem item, bool appendLine = false)
    {
        if (appendLine)
            item.Message += Environment.NewLine;

        if (_dispatcher.CheckAccess())
        {
            _target.Add(item);
        }
        else
        {
            _dispatcher.BeginInvoke(new Action(() => _target.Add(item)));
        }
    }
}