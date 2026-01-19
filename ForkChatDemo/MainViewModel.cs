using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using ForkChatDemo.Models;

namespace ForkChatDemo.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    public ObservableCollection<ChatNode> RootNodes { get; } = new();

    private ChatNode? _selectedNode;
    public ChatNode? SelectedNode
    {
        get => _selectedNode;
        set { _selectedNode = value; OnPropertyChanged(); OnPropertyChanged(nameof(SelectedPath)); }
    }

    // 选中节点的完整路径（用于右侧预览）
    public string SelectedPath
    {
        get
        {
            if (SelectedNode == null) return "点击左侧节点查看完整路径";
            var path = new List<string>();
            var node = SelectedNode;
            while (node != null)
            {
                path.Insert(0, $"[{node.Role}] {node.Content}");
                node = node.Parent;
            }
            return string.Join("\n\n↓\n\n", path);
        }
    }

    public MainViewModel()
    {
        LoadSampleData();
    }

    private void LoadSampleData()
    {
        // ======== 构建示例对话树 ========
        
        // 第一条对话链
        var root1 = new ChatNode(ChatRole.User, "你好，请介绍一下WPF的MVVM模式");
        RootNodes.Add(root1);

        var a1 = root1.AddChild(ChatRole.Assistant, 
            "MVVM（Model-View-ViewModel）是WPF中常用的架构模式。它将应用程序分为三层：Model负责数据和业务逻辑，View负责UI展示，ViewModel作为中间层处理视图逻辑和数据绑定。这种模式的核心优势是解耦和可测试性。");

        // 用户继续追问（主线）
        var u2 = a1.AddChild(ChatRole.User, "能详细说说数据绑定吗？");
        
        var a2 = u2.AddChild(ChatRole.Assistant,
            "WPF数据绑定是连接UI和数据的桥梁。主要特性包括：\n1. OneWay - 源到目标单向\n2. TwoWay - 双向绑定\n3. OneTime - 一次性绑定\n绑定还支持转换器(IValueConverter)、验证规则等高级特性。");

        // ======== 分叉1：从 a1 开始另一个话题 ========
        var u2_fork1 = a1.AddChild(ChatRole.User, "那Command模式呢？怎么实现按钮点击？");
        
        var a2_fork1 = u2_fork1.AddChild(ChatRole.Assistant,
            "ICommand接口是MVVM中处理用户交互的核心。通常我们实现RelayCommand或使用CommunityToolkit.Mvvm的[RelayCommand]特性。命令绑定到Button.Command属性，当按钮点击时自动执行。");

        var u3_fork1 = a2_fork1.AddChild(ChatRole.User, "有代码示例吗？");
        
        a2_fork1.AddChild(ChatRole.Assistant,
            "当然！\n```csharp\npublic ICommand ClickCommand => new RelayCommand(() => {\n    MessageBox.Show(\"Clicked!\");\n});\n```\nXAML中：`<Button Command=\"{Binding ClickCommand}\"/>`");

        // ======== 分叉2：从 a1 开始第三个话题 ========
        var u2_fork2 = a1.AddChild(ChatRole.User, "MVVM和MVC有什么区别？");
        
        u2_fork2.AddChild(ChatRole.Assistant,
            "主要区别：\n1. MVC的Controller直接处理用户输入，MVVM通过数据绑定\n2. MVVM的ViewModel不知道View的存在，解耦更彻底\n3. WPF的绑定机制让MVVM更自然");

        // ======== 主线继续深入 ========
        var u3 = a2.AddChild(ChatRole.User, "IValueConverter怎么用？");
        
        var a3 = u3.AddChild(ChatRole.Assistant,
            "实现IValueConverter接口的Convert和ConvertBack方法。例如布尔转可见性：\n```csharp\npublic object Convert(object value, ...) {\n    return (bool)value ? Visibility.Visible : Visibility.Collapsed;\n}\n```");

        // 从 a3 再分叉
        a3.AddChild(ChatRole.User, "能用于多值绑定吗？").AddChild(ChatRole.Assistant,
            "可以！使用IMultiValueConverter和MultiBinding。它接收多个绑定值数组，返回单个结果。适合需要组合多个属性的场景。");

        a3.AddChild(ChatRole.User, "有没有现成的转换器库？").AddChild(ChatRole.Assistant,
            "推荐：\n1. ValueConverters.NET (NuGet)\n2. CalcBinding - 支持表达式绑定\n3. 自己维护一个常用转换器的静态资源字典");

        // ======== 第二条独立对话 ========
        var root2 = new ChatNode(ChatRole.User, "帮我写一个快速排序算法");
        RootNodes.Add(root2);

        var root2_a1 = root2.AddChild(ChatRole.Assistant,
            "```csharp\nvoid QuickSort(int[] arr, int low, int high) {\n    if (low < high) {\n        int pi = Partition(arr, low, high);\n        QuickSort(arr, low, pi - 1);\n        QuickSort(arr, pi + 1, high);\n    }\n}\n```");

        root2_a1.AddChild(ChatRole.User, "能优化一下吗？").AddChild(ChatRole.Assistant,
            "优化版本：\n1. 小数组切换插入排序\n2. 三数取中选pivot\n3. 尾递归优化\n需要我展示具体代码吗？");

        root2_a1.AddChild(ChatRole.User, "时间复杂度是多少？").AddChild(ChatRole.Assistant,
            "快速排序复杂度：\n- 平均：O(n log n)\n- 最坏：O(n²)（已排序数组）\n- 空间：O(log n)（递归栈）\n通过随机化pivot可以避免最坏情况。");
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}