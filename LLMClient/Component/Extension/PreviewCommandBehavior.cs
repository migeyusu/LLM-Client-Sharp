using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Microsoft.Xaml.Behaviors;

namespace LLMClient.Component.Extension;

public class PreviewCommandBehavior : Behavior<TextBoxBase>
{
    public ICommand? RedirectCommand { get; set; }

    public static readonly DependencyProperty ExecuteCommandProperty = DependencyProperty.Register(
        nameof(ExecuteCommand), typeof(ICommand), typeof(PreviewCommandBehavior),
        new PropertyMetadata(default(ICommand)));

    public ICommand ExecuteCommand
    {
        get { return (ICommand)GetValue(ExecuteCommandProperty); }
        set { SetValue(ExecuteCommandProperty, value); }
    }

    protected override void OnAttached()
    {
        base.OnAttached();
        // 挂载静态处理程序
        CommandManager.AddPreviewExecutedHandler(AssociatedObject, Handler);
    }

    private void Handler(object sender, ExecutedRoutedEventArgs e)
    {
        if (e.Command == RedirectCommand)
        {
            ExecuteCommand?.Execute(e.Parameter);
        }
    }

    protected override void OnDetaching()
    {
        // 必须在卸载时取消挂载，防止内存泄漏
        CommandManager.RemovePreviewExecutedHandler(AssociatedObject, Handler);
        base.OnDetaching();
    }

    private void OnPasting(object sender, DataObjectPastingEventArgs e)
    {
        if (e.DataObject.GetDataPresent(DataFormats.FileDrop))
        {
            if (Clipboard.ContainsImage())
            {
                return;
            }

            if (e.DataObject.GetData(DataFormats.FileDrop) is string[] paths)
            {
                if (RedirectCommand?.CanExecute(paths) == true)
                {
                    RedirectCommand.Execute(paths);
                    // 关键：拦截系统默认行为，防止路径字符串被插进输入框
                    e.CancelCommand();
                    e.Handled = true;
                    return;
                }
            }
        }

        // 场景 2：强制纯文本粘贴 (可选，防止 HTML 格式污染)
        /*if (e.DataObject.GetDataPresent(DataFormats.UnicodeText))
        {
            /*
            var text = (string)e.DataObject.GetData(DataFormats.UnicodeText);
            e.CancelCommand();
            AssociatedObject.BeginChange();
            AssociatedObject.Selection.Text = text;
            AssociatedObject.EndChange();
            #1#
        }*/
    }
}