using System.Collections.Specialized;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace LLMClient.Component.CustomControl;

public class ListBoxEx : ListBox
{
    static ListBoxEx()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(ListBoxEx), new FrameworkPropertyMetadata(typeof(ListBox)));
    }

    public ListBoxEx()
    {
        this.Loaded += OnLoaded;
    }


    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        UpdateCurrentVisibleItem();
    }

    public static readonly DependencyProperty CurrentVisibleItemProperty = DependencyProperty.Register(
        nameof(CurrentVisibleItem), typeof(object), typeof(ListBoxEx),
        new PropertyMetadata(null, OnCurrentVisibleItemChanged));

    public object? CurrentVisibleItem
    {
        get { return (object)GetValue(CurrentVisibleItemProperty); }
        set { SetValue(CurrentVisibleItemProperty, value); }
    }


    private static void OnCurrentVisibleItemChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var newValue = e.NewValue;
        if (d is ListBoxEx lbEx && newValue != null)
        {
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

    private void UpdateCurrentVisibleItem()
    {
        // 获取可视区域内的第一个项
        var currentItem = GetItemAtViewport();
        if (currentItem != null && !object.Equals(currentItem, this.CurrentVisibleItem))
        {
            try
            {
                // 设置标记，避免触发重复滚动
                SetIsScrollChangeInProgress(this, true);
                this.CurrentVisibleItem = currentItem;
            }
            finally
            {
                // 确保标记被重置
                SetIsScrollChangeInProgress(this, false);
            }
        }
    }


    private object? GetItemAtViewport()
    {
        var itemCollection = this.Items;
        if (itemCollection.Count == 0) return null;

        var scrollViewer = this.ScrollViewer;
        if (scrollViewer == null) return null;

        // 获取可视区域的垂直位置
        var verticalOffset = scrollViewer.VerticalOffset;
        var viewportHeight = scrollViewer.ViewportHeight;
        var topPoint = verticalOffset; // 视域顶部位置
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

                var itemTop = containerRect.Top;
                var itemBottom = containerRect.Bottom;

                // 判断项是否在视域内（部分可见也算）
                if (itemBottom > topPoint && itemTop < topPoint + viewportHeight)
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