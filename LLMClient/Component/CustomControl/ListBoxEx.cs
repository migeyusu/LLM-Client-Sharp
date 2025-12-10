using System.Collections.Specialized;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;

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
                lbEx.ScrollIntoView(newValue);
            }
        }
    }

    private ScrollViewer? _scrollViewer;

    protected ScrollViewer? ScrollViewer
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
        // 获取可视区域中心的项
        var currentItem = GetItemAtViewportBottom();
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


    private object? GetItemAtViewportBottom()
    {
        var itemCollection = this.Items;
        if (itemCollection.Count == 0) return null;

        var scrollViewer = this.ScrollViewer;
        if (scrollViewer == null) return null;

        // 获取可视区域的垂直位置
        var verticalOffset = scrollViewer.VerticalOffset;
        var viewportHeight = scrollViewer.ViewportHeight;
        var bottomPoint = verticalOffset + viewportHeight;
        var containerGenerator = this.ItemContainerGenerator;
        // 查找底部位置的项
        for (var i = 0; i < itemCollection.Count; i++)
        {
            var item = itemCollection[i];
            if (containerGenerator.ContainerFromItem(item) is FrameworkElement container)
            {
                // 获取项在ListBox中的位置
                var itemPos = container.TransformToAncestor(scrollViewer).Transform(new Point(0, verticalOffset));
                var itemTop = itemPos.Y;
                var itemBottom = container.ActualHeight + itemTop;
                if (bottomPoint >= itemTop && bottomPoint <= itemBottom)
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