using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using LLMClient.UI.Component.Utility;

namespace LLMClient.ToolCall;

public partial class MCPServiceCollectionView : UserControl
{
    public MCPServiceCollectionView()
    {
        InitializeComponent();
    }

    private McpServiceCollection? McpServiceCollection => this.DataContext as McpServiceCollection;

    private void DeleteItem_OnExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        if (e.Parameter is McpServerItem mcpServerItem)
        {
            if (MessageBox.Show("确认删除该服务？", "确认", MessageBoxButton.OKCancel, MessageBoxImage.Question) ==
                MessageBoxResult.OK)
            {
                this.McpServiceCollection?.DeleteServerItem(mcpServerItem);
            }
        }
    }

    private async void MCPServiceCollectionView_OnLoaded(object sender, RoutedEventArgs e)
    {
        // 第一次临时实例化时 IsVisible == false，或者 PresentationSource == null
        if (!IsVisible || PresentationSource.FromVisual(this) == null)
            return;
        var mcpServiceCollection = this.McpServiceCollection;
        if (mcpServiceCollection != null)
        {
            try
            {
                await mcpServiceCollection.EnsureAsync();
            }
            catch (Exception exception)
            {
                MessageEventBus.Publish("加载 MCP 服务失败: " + exception.Message);
            }
        }
    }
}