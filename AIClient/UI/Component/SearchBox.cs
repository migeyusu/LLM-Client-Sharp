using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace LLMClient.UI.Component;

public class SearchBox : TextBox
{
    static SearchBox()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(SearchBox), new FrameworkPropertyMetadata(typeof(SearchBox)));
    }

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
}