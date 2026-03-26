using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using LLMClient.Component.CustomControl;
using LLMClient.Component.Render;
using LLMClient.Component.Utility;
using LLMClient.Dialog.Models;

namespace LLMClient.Dialog;

public partial class SessionView : UserControl
{
    public SessionView()
    {
        InitializeComponent();
    }

    private DialogSessionViewModel? ViewModel => (DialogSessionViewModel?)DataContext;

    private void OnDeleteExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        try
        {
            if (e.Parameter is IDialogItem dialogViewItem)
            {
                ViewModel?.DeleteItem(dialogViewItem);
            }
        }
        catch (Exception exception)
        {
            MessageEventBus.Publish($"删除失败：{exception.Message}");
        }
    }

    private void OnExcludeExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        try
        {
            if (ViewModel == null)
            {
                return;
            }

            if (e.Parameter is RequestViewItem requestViewItem)
            {
                ViewModel.CutContext(requestViewItem);
            }
        }
        catch (Exception exception)
        {
            MessageEventBus.Publish($"剪切失败：{exception.Message}");
        }
    }

    private void SearchBox_OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if ((bool)e.NewValue)
        {
            var searchBox = (Control)sender;
            searchBox.Focus();
        }
    }

    private void SearchBox_OnLostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is SearchBox searchBox && !searchBox.IsKeyboardFocusWithin)
        {
            if (ViewModel == null)
            {
                return;
            }

            if (string.IsNullOrEmpty(ViewModel.SearchText))
            {
                ViewModel.IsSearchVisible = false;
            }
        }
    }

    private void OnScrollToNextCodeBlock(object sender, ExecutedRoutedEventArgs e)
    {
        ScrollToCodeBlock(true);
    }

    private void OnScrollToPreviousCodeBlock(object sender, ExecutedRoutedEventArgs e)
    {
        ScrollToCodeBlock(false);
    }

    private void ScrollToCodeBlock(bool isNext)
    {
        var currentItem = DialogItemList.CurrentViewItem;
        if (currentItem == null) return;

        var container = DialogItemList.ItemContainerGenerator.ContainerFromItem(currentItem) as FrameworkElement;
        if (container == null) return;

        var scrollViewer = DialogItemList.FindVisualChild<ScrollViewer>();
        if (scrollViewer == null) return;

        var codeBlocks = FindVisualChildren<ContentControl>(container)
            .Where(cc => cc.Content is DisplayCodeViewModel)
            .ToList();

        if (codeBlocks.Count == 0) return;

        // Find blocks relative to viewport
        var relativeBlocks = codeBlocks.Select(b =>
            {
                try
                {
                    var transform = b.TransformToAncestor(scrollViewer);
                    var point = transform.Transform(new Point(0, 0));
                    return new { Block = b, Top = point.Y };
                }
                catch
                {
                    return null;
                }
            })
            .Where(x => x != null)
            .OrderBy(x => x!.Top)
            .ToList();

        if (isNext)
        {
            // Find first block strictly below the top slop
            var target = relativeBlocks.FirstOrDefault(x => x!.Top > 10);
            if (target != null)
            {
                scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset + target.Top);
            }
        }
        else
        {
            // Find last block strictly above the top slop
            var target = relativeBlocks.LastOrDefault(x => x!.Top < -10);
            if (target != null)
            {
                scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset + target.Top);
            }
        }
    }
    

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject? depObj) where T : DependencyObject
    {
        if (depObj == null) yield break;

        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(depObj, i);
            if (child is T t)
            {
                yield return t;
            }

            foreach (T childOfChild in FindVisualChildren<T>(child))
            {
                yield return childOfChild;
            }
        }
    }

    private void DeleteInteraction_OnExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        try
        {
            if (e.Parameter is IRequestItem requestViewItem)
            {
                ViewModel?.DeleteInteraction(requestViewItem);
            }
        }
        catch (Exception exception)
        {
            MessageEventBus.Publish($"删除失败：{exception.Message}");
        }
    }
}