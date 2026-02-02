using System.Windows;

namespace LLMClient.Dialog.Controls;

public static class GraphProps
{
    // 用于标识节点的唯一 ID
    public static readonly DependencyProperty NodeIdProperty =
        DependencyProperty.RegisterAttached("NodeId", typeof(Guid), typeof(GraphProps), new PropertyMetadata(Guid.Empty));

    public static void SetNodeId(DependencyObject element, Guid value) => element.SetValue(NodeIdProperty, value);
    public static Guid GetNodeId(DependencyObject element) => (Guid)element.GetValue(NodeIdProperty);

    // 用于标识父节点的 ID
    public static readonly DependencyProperty ParentIdProperty =
        DependencyProperty.RegisterAttached("ParentId", typeof(Guid?), typeof(GraphProps), new PropertyMetadata(null));

    public static void SetParentId(DependencyObject element, Guid? value) => element.SetValue(ParentIdProperty, value);
    public static Guid? GetParentId(DependencyObject element) => (Guid?)element.GetValue(ParentIdProperty);
}