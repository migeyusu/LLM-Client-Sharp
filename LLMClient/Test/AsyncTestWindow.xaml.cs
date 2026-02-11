using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Documents;

namespace LLMClient.Test;

public partial class AsyncTestWindow : Window, INotifyPropertyChanged
{
    private FlowDocument? _document;

    public AsyncTestWindow()
    {
        this.DataContext = this;
        InitializeComponent();
        this.Loaded += OnLoaded;
    }

    private string GenerateHugeXamlData()
    {
        // 简单的生成一个庞大的 XAML FlowDocument 结构
        var sb = new System.Text.StringBuilder();
        sb.Append("<FlowDocument xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'>");
        sb.Append("<Paragraph><Bold FontSize='24'>独立线程渲染的大型文档</Bold></Paragraph>");
        
        for (int i = 0; i < 5000; i++)
        {
            sb.Append($"<Paragraph>这是第 {i} 段内容。由于是在独立线程渲染，主界面的按钮依然可以点击，窗口依然可以拖动，不会出现未响应的情况。</Paragraph>");
        }
        
        sb.Append("</FlowDocument>");
        return sb.ToString();
    }
    
    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        /*var testStr =
            "## Overview\n\n- **Files**: 327\n- **Types**: 827\n- **Methods**: 2238\n- **Lines of Code**: 35,570\n\n### LLMClient\n\n- **Type**: WindowsApplication\n- **Relative Path**: `.`\n- **Frameworks**: netstandard\n- **Language**: C#\n- **Language Version**: 12.0\n- **Statistics**: 827 types, 2238 methods\n\n#### Package References\n- automapper (15.0.1)\n- avalonedit (6.3.1.120)\n- azure.ai.textanalytics (5.3.0)\n- azure.core (1.33.0)\n- cefsharp.common.netcore (141.0.110)\n- cefsharp.wpf.netcore (141.0.110)\n- closedxml (0.104.2)\n- closedxml.parser (1.2.0)\n- communitytoolkit.mvvm (8.4.0)\n- diffplex (1.9.0)\n- ... and 173 more";
        var flowDocument = await Task.Run((() =>
        {
            /*CustomRenderer customRenderer = CustomRenderer.NewRenderer(new FlowDocument());
            customRenderer.RenderRaw(testStr);
            return customRenderer.Document;#1#
        
        }));
        this.Document = flowDocument;*/

        string hugeXaml = GenerateHugeXamlData();

        // 2. 创建多线程宿主
        // 当这行代码执行时，WPF 的 Document 排版引擎将在独立线程运行
        var host = new ThreadedDocumentHost(hugeXaml);

        // 3. 添加到 UI 容器（假设 XAML 里有个叫 ContainerBorder 的 Border）
        ContainerBorder.Child = host;        
        
    }

    
    // 模拟生成大量内容的方法（在后台线程安全执行）

    public FlowDocument? Document
    {
        get => _document;
        set => SetField(ref _document, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}