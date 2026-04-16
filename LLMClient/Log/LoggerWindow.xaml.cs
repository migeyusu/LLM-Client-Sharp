using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using LLMClient.Component.Utility;

namespace LLMClient.Log;

public partial class LoggerWindow : Window
{
    public int MaxCount { get; set; }
    
    public ObservableCollection<LogItem> LogItems { get; set; } = new ObservableCollection<LogItem>();

    private readonly ObservableCollectionTraceListener _listener;

    public bool CanClose { get; set; } = false;

    public LoggerWindow()
    {
        this.DataContext = this;
        InitializeComponent();
        _listener = new ObservableCollectionTraceListener(LogItems);
        this.Loaded += OnLoaded;
        this.Unloaded += OnUnloaded;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Trace.Listeners.Remove(_listener);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // 把自定义监听器挂到 Trace
        Trace.Listeners.Add(_listener);
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