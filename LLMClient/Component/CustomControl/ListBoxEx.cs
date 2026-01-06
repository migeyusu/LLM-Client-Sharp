using System.Collections.Specialized;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LLMClient.Component.CustomControl;

public class ListBoxEx : ListBox
{
    public static ICommand ScrollToPreviousItemCommand = new RoutedCommand("ScrollToPreviousItem", typeof(ListBoxEx));

    public static ICommand ScrollToNextItemCommand = new RoutedCommand("ScrollToNextItem", typeof(ListBoxEx));

    public static ICommand ScrollToTopCommand = new RoutedCommand("ScrollToTopItem", typeof(ListBoxEx));

    public static ICommand ScrollToBottomCommand = new RoutedCommand("ScrollToBottomItem", typeof(ListBoxEx));

    static ListBoxEx()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(ListBoxEx), new FrameworkPropertyMetadata(typeof(ListBox)));
        CommandManager.RegisterClassCommandBinding(typeof(ListBoxEx),
            new CommandBinding(ScrollToPreviousItemCommand, ExecutedScrollToPrevious,
                CanExecuteExecutedScrollToPrevious));
        CommandManager.RegisterClassCommandBinding(typeof(ListBoxEx),
            new CommandBinding(ScrollToNextItemCommand, ExecutedScrollToNext, CanExecuteExecutedScrollToNext));
        CommandManager.RegisterClassCommandBinding(typeof(ListBoxEx),
            new CommandBinding(ScrollToTopCommand, ExecutedScrollToTop,((sender, args) =>
            {
                var listBoxEx = (ListBoxEx)sender;
                args.CanExecute = listBoxEx.Items.Count > 0 && (listBoxEx.ScrollViewer?.VerticalOffset > 0 == true);
            })));
        CommandManager.RegisterClassCommandBinding(typeof(ListBoxEx),
            new CommandBinding(ScrollToBottomCommand, ExecutedScrollToBottom, ((sender, args) =>
            {
                var listBoxEx = (ListBoxEx)sender;
                var scrollViewer = listBoxEx.ScrollViewer;
                args.CanExecute = listBoxEx.Items.Count > 0 && (scrollViewer != null &&
                                                                 scrollViewer.VerticalOffset <
                                                                 scrollViewer.ExtentHeight - scrollViewer.ViewportHeight);
            })));
    }

    private static void CanExecuteExecutedScrollToPrevious(object sender, CanExecuteRoutedEventArgs e)
    {
        var listBoxEx = (ListBoxEx)sender;
        e.CanExecute = listBoxEx.Items.Count > 0 && listBoxEx.CurrentViewIndex > 0;
    }

    private static void CanExecuteExecutedScrollToNext(object sender, CanExecuteRoutedEventArgs e)
    {
        var listBoxEx = (ListBoxEx)sender;
        e.CanExecute = listBoxEx.Items.Count > 0 && listBoxEx.CurrentViewIndex < listBoxEx.Items.Count - 1;
    }

    private static void ExecutedScrollToBottom(object sender, ExecutedRoutedEventArgs e)
    {
        var listBoxEx = (ListBoxEx)sender;
        listBoxEx?.ScrollViewer?.ScrollToBottom();
    }

    private static void ExecutedScrollToTop(object sender, ExecutedRoutedEventArgs e)
    {
        var listBoxEx = (ListBoxEx)sender;
        listBoxEx?.ScrollViewer?.ScrollToTop();
    }

    private static void ExecutedScrollToPrevious(object sender, ExecutedRoutedEventArgs e)
    {
        ((ListBoxEx)sender)?.ScrollToPreviousItem();
    }

    private static void ExecutedScrollToNext(object sender, ExecutedRoutedEventArgs e)
    {
        ((ListBoxEx)sender)?.ScrollToNextItem();
    }


    public ListBoxEx()
    {
        this.Loaded += OnLoaded;
    }


    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        UpdateCurrentVisibleItem();
    }

    public static readonly DependencyProperty ScrollCoherenceProperty = DependencyProperty.Register(
        nameof(ScrollCoherence), typeof(uint), typeof(ListBoxEx), new PropertyMetadata(50u));

    /// <summary>
    /// 滚动容忍度，表示在滚动时允许的误差范围，单位为像素。
    /// </summary>
    public uint ScrollCoherence
    {
        get { return (uint)GetValue(ScrollCoherenceProperty); }
        set { SetValue(ScrollCoherenceProperty, value); }
    }

    public static readonly DependencyProperty CurrentViewItemProperty = DependencyProperty.Register(
        nameof(CurrentViewItem), typeof(object), typeof(ListBoxEx),
        new PropertyMetadata(null, OnCurrentVisibleItemChanged));

    public object? CurrentViewItem
    {
        get { return (object)GetValue(CurrentViewItemProperty); }
        set { SetValue(CurrentViewItemProperty, value); }
    }

    public int CurrentViewIndex { get; private set; }

    private static void OnCurrentVisibleItemChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var newValue = e.NewValue;
        if (d is ListBoxEx lbEx && newValue != null)
        {
            lbEx.CurrentViewIndex = lbEx.Items.IndexOf(newValue);
            if (!IsScrollChangeInProgress(lbEx))
            {
                if (lbEx.ItemContainerGenerator.ContainerFromItem(newValue) is ListBoxItem listBoxItem)
                {
                    var scrollViewer = lbEx.ScrollViewer;
                    if (scrollViewer == null)
                    {
                        return;
                    }

                    var transform = listBoxItem.TransformToVisual(scrollViewer);
                    var position = transform.Transform(new Point(0, 0));
                    scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset + position.Y);
                    // listBoxItem.BringIntoView(new Rect(new Point(0,0), listBoxItem.RenderSize));
                }
                else
                {
                    // 如果容器未生成，先滚动到该项使其可见，再获取容器
                    lbEx.ScrollIntoView(newValue);
                    lbEx.Dispatcher.InvokeAsync(() =>
                    {
                        if (lbEx.ItemContainerGenerator.ContainerFromItem(newValue) is ListBoxItem lbi)
                        {
                            var scrollViewer = lbEx.ScrollViewer;
                            if (scrollViewer == null)
                            {
                                return;
                            }

                            var transform = lbi.TransformToVisual(scrollViewer);
                            var position = transform.Transform(new Point(0, 0));
                            scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset + position.Y);
                        }
                    }, DispatcherPriority.Render);
                }
            }
        }
    }

    private ScrollViewer? _scrollViewer;

    private ScrollViewer? ScrollViewer
    {
        get
        {
            if (_scrollViewer == null)
            {
                var propertyInfo =
                    typeof(ListBox).GetProperty("ScrollHost", BindingFlags.NonPublic | BindingFlags.Instance);
                var scrollViewer = propertyInfo?.GetValue(this) as ScrollViewer;
                if (scrollViewer != null)
                {
                    scrollViewer.ScrollChanged += ListBox_ScrollChanged;
                    _scrollViewer = scrollViewer;
                }
            }

            return _scrollViewer;
        }
    }


    protected override void OnTemplateChanged(ControlTemplate oldTemplate, ControlTemplate newTemplate)
    {
        this._scrollViewer = null;
        base.OnTemplateChanged(oldTemplate, newTemplate);
    }

    protected override void OnItemsChanged(NotifyCollectionChangedEventArgs e)
    {
        base.OnItemsChanged(e);
        UpdateCurrentVisibleItem();
    }

    private void ListBox_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        UpdateCurrentVisibleItem();
    }

    private void ScrollToPreviousItem()
    {
        var itemCollection = this.Items;
        if (itemCollection.Count == 0) return;
        var currentViewItem = this.CurrentViewItem;
        if (currentViewItem == null)
        {
            this.CurrentViewItem = itemCollection[0];
            return;
        }

        // 如果当前项的垂直位置已经在视域顶部以上一定距离，则不变更当前项，而是直接滚动到当前项顶部
        var currentItemRect = GetItemRelativeRect(currentViewItem);
        if (currentItemRect != null && currentItemRect.Value.Top < -this.ScrollCoherence) //应当为负值
        {
            var scrollViewer = this.ScrollViewer!;
            scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset + currentItemRect.Value.Top);
            return;
        }

        var currentIndex = itemCollection.IndexOf(currentViewItem);
        var previousIndex = Math.Max(0, currentIndex - 1);
        var previousItem = itemCollection[previousIndex];
        this.CurrentViewItem = previousItem;
    }

    private void ScrollToNextItem()
    {
        var itemCollection = this.Items;
        if (itemCollection.Count == 0) return;
        var currentViewItem = this.CurrentViewItem;
        if (currentViewItem == null)
        {
            this.CurrentViewItem = itemCollection[0];
            return;
        }

        var scrollViewer = this.ScrollViewer!;
        // 如果当前项的垂直位置已经在视域底部以上一定距离，则不变更当前项，而是直接滚动到当前底部
        var currentItemRect = GetItemRelativeRect(currentViewItem);
        if (currentItemRect != null &&
            currentItemRect.Value.Bottom > scrollViewer.ViewportHeight + this.ScrollCoherence)
        {
            var offsetChange = currentItemRect.Value.Bottom - scrollViewer.ViewportHeight;
            scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset + offsetChange);
            return;
        }

        var currentIndex = itemCollection.IndexOf(currentViewItem);
        var nextIndex = Math.Min(itemCollection.Count - 1, currentIndex + 1);
        var nextItem = itemCollection[nextIndex];
        this.CurrentViewItem = nextItem;
    }

    private void UpdateCurrentVisibleItem()
    {
        // 获取可视区域内的第一个项
        var currentItem = GetItemAtViewport();
        if (currentItem != null && !object.Equals(currentItem, this.CurrentViewItem))
        {
            try
            {
                // 设置标记，避免触发重复滚动
                SetIsScrollChangeInProgress(this, true);
                this.CurrentViewItem = currentItem;
            }
            finally
            {
                // 确保标记被重置
                SetIsScrollChangeInProgress(this, false);
            }
        }
    }

    private Rect? GetItemRelativeRect(object item)
    {
        var containerGenerator = this.ItemContainerGenerator;
        if (containerGenerator.ContainerFromItem(item) is FrameworkElement container)
        {
            var scrollViewer = this.ScrollViewer;
            if (scrollViewer == null) return null;
            var transform = container.TransformToAncestor(scrollViewer);
            var containerRect =
                transform.TransformBounds(new Rect(0, 0, container.ActualWidth, container.ActualHeight));
            return containerRect;
        }

        return null;
    }

    private object? GetItemAtViewport()
    {
        var itemCollection = this.Items;
        if (itemCollection.Count == 0) return null;

        var scrollViewer = this.ScrollViewer;
        if (scrollViewer == null) return null;
        var containerGenerator = this.ItemContainerGenerator;
        // 从上到下查找第一个在视域内的项
        for (var i = 0; i < itemCollection.Count; i++)
        {
            var item = itemCollection[i];
            if (containerGenerator.ContainerFromItem(item) is FrameworkElement container)
            {
                // 正确获取容器相对于滚动视图的位置
                var transform = container.TransformToAncestor(scrollViewer);
                var containerRect =
                    transform.TransformBounds(new Rect(0, 0, container.ActualWidth, container.ActualHeight));
                var itemBottom = containerRect.Bottom;
                // 判断项是否在视域内（部分可见也算）
                if (itemBottom > 0)
                {
                    return item;
                }
            }
        }

        return null;
    }

    // 用于防止滚动时重复触发
    private static readonly DependencyProperty IsScrollChangeInProgressProperty =
        DependencyProperty.RegisterAttached(
            "IsScrollChangeInProgress",
            typeof(bool),
            typeof(ListBoxEx),
            new PropertyMetadata(false));

    private static bool IsScrollChangeInProgress(ListBox listBox)
    {
        return (bool)listBox.GetValue(IsScrollChangeInProgressProperty);
    }

    private static void SetIsScrollChangeInProgress(ListBox listBox, bool value)
    {
        listBox.SetValue(IsScrollChangeInProgressProperty, value);
    }
}