using System.Windows;
using LLMClient.Component.ViewModel.Base;

// 引用你的数据结构命名空间

namespace LLMClient.Dialog.Controls;

// 节点的显示模型
public class GraphNodeViewModel : BaseViewModel
{
    public IDialogItem Data { get; }
    
    // 只有当有分叉时才显示底部的橙色圆点
    public bool ShowForkDot => Data.Children.Count > 1;

    public GraphNodeViewModel(IDialogItem data)
    {
        Data = data;
    }
}