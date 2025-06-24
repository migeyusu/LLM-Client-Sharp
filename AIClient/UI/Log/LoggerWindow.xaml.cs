using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;

namespace LLMClient.UI.Log;

public partial class LoggerWindow : Window
{
    public ObservableCollection<LogItem> LogItems { get; set; } = new ObservableCollection<LogItem>();

    public bool CanClose { get; set; } = false;

    public LoggerWindow()
    {
        this.DataContext = this;
        InitializeComponent();
        // 把自定义监听器挂到 Trace
        Trace.Listeners.Add(
            new ObservableCollectionTraceListener(LogItems, Application.Current.Dispatcher));
        Trace.AutoFlush = true; // 写完立即刷新，避免丢日志
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        e.Cancel = !CanClose;
        base.OnClosing(e);
        if (!CanClose)
        {
            this.Hide();
        }
    }

    public void Shutdown()
    {
        this.CanClose = true;
        this.Close();
    }

    private void Clear_OnClick(object sender, RoutedEventArgs e)
    {
        LogItems.Clear();
    }
}