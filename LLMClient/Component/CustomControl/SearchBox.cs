using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace LLMClient.Component.CustomControl;

[TemplatePart(Name = "PART_InputBox", Type = typeof(TextBox))]
public class SearchBox : TextBox
{
    static SearchBox()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(SearchBox), new FrameworkPropertyMetadata(typeof(SearchBox)));
    }

    private TextBox? _inputHeader;
    
    public static readonly DependencyProperty SearchCommandProperty = DependencyProperty.Register(
        nameof(SearchCommand), typeof(ICommand), typeof(SearchBox), new PropertyMetadata(default(ICommand)));

    public ICommand SearchCommand
    {
        get { return (ICommand)GetValue(SearchCommandProperty); }
        set { SetValue(SearchCommandProperty, value); }
    }

    public static readonly DependencyProperty GoToNextCommandProperty = DependencyProperty.Register(
        nameof(GoToNextCommand), typeof(ICommand), typeof(SearchBox), new PropertyMetadata(default(ICommand)));

    public ICommand GoToNextCommand
    {
        get { return (ICommand)GetValue(GoToNextCommandProperty); }
        set { SetValue(GoToNextCommandProperty, value); }
    }

    public static readonly DependencyProperty GoToPreviousCommandProperty = DependencyProperty.Register(
        nameof(GoToPreviousCommand), typeof(ICommand), typeof(SearchBox), new PropertyMetadata(default(ICommand)));

    public ICommand GoToPreviousCommand
    {
        get { return (ICommand)GetValue(GoToPreviousCommandProperty); }
        set { SetValue(GoToPreviousCommandProperty, value); }
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _inputHeader = GetTemplateChild("PART_InputBox") as TextBox;
    }

    protected override void OnGotFocus(RoutedEventArgs e)
    {
        base.OnGotFocus(e);
        // 只有当焦点落在 SearchBox 本身（而不是已经在内部 TextBox 上）时才转移
        if (Equals(e.OriginalSource, this) && _inputHeader != null)
        {
            _inputHeader.Focus();
            // 防止事件递归死循环，通常 Focus() 会触发新的 GotFocus，但源变为 _inputHeader
        }
    }
}