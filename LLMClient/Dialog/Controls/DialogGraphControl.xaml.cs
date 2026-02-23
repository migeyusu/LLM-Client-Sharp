using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using LLMClient.Dialog.Models;
using MaterialDesignThemes.Wpf;

namespace LLMClient.Dialog.Controls;

public partial class DialogGraphControl : UserControl
{
    public DialogGraphControl()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        this.Width = double.NaN;
        this.Height = double.NaN;
    }

    private void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        // 锁定当前大小，防止后续因内容缩放导致控件尺寸跳变
        if (ActualWidth > 0 && ActualHeight > 0)
        {
            Width = ActualWidth;
            Height = ActualHeight;
        }
    }

    private void ZoomIn_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        ZoomSlider.Value = Math.Min(ZoomSlider.Maximum, ZoomSlider.Value + 0.2);
    }

    private void ZoomOut_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        ZoomSlider.Value = Math.Max(ZoomSlider.Minimum, ZoomSlider.Value - 0.2);
    }

    private void ZoomReset_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        ZoomSlider.Value = 1.0;
    }

    private void Control_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (this.DataContext is not DialogGraphViewModel dialogGraphViewModel) return;
        if (!dialogGraphViewModel.CanSelect) return;
        dialogGraphViewModel.SelectCommand.Execute(null);
        DialogHost.CloseDialogCommand.Execute(null, null);
    }

    private void Delete_CommandBinding_OnExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        if (this.DataContext is not DialogGraphViewModel dialogGraphViewModel) return;
        if (e.Parameter is not RequestViewItem requestViewItem) return;
        dialogGraphViewModel.DeleteRequestItem(requestViewItem);
    }
}