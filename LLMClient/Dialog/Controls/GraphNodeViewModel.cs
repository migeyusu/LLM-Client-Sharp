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

// 连线的显示模型
public class GraphConnectionViewModel : BaseViewModel
{
    // 是否是主分支连线（决定颜色：灰色 vs 橙色）
    public bool IsMainBranch { get; set; }

    // SVG 路径数据字符串
    public string PathData { get; set; } = string.Empty;
}