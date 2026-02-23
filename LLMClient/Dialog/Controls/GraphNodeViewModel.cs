using LLMClient.Component.ViewModel;
using LLMClient.Dialog.Models;

// 引用你的数据结构命名空间

namespace LLMClient.Dialog.Controls;

// 节点的显示模型
public class GraphNodeViewModel : SelectableViewModel<IDialogItem>
{
    // 只有当有分叉时才显示底部的橙色圆点
    public bool ShowForkDot => Data.Children.Count > 1;

    public GraphNodeViewModel(IDialogItem data) : base(data)
    {
    }
}