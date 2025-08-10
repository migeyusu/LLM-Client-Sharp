using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace LLMClient.MCP;

public partial class MCPServiceCollectionView : UserControl
{
    public MCPServiceCollectionView()
    {
        InitializeComponent();
    }

    private McpServiceCollection? McpServiceCollection => this.DataContext as McpServiceCollection;

    private void DeleteItem_OnExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        var mcpServiceCollection = this.McpServiceCollection;
        mcpServiceCollection?.DeleteServerItem(e.Parameter as McpServerItem);
    }

    private async void MCPServiceCollectionView_OnLoaded(object sender, RoutedEventArgs e)
    {
        // 第一次临时实例化时 IsVisible == false，或者 PresentationSource == null
        if (!IsVisible || PresentationSource.FromVisual(this) == null)
            return;
        var mcpServiceCollection = this.McpServiceCollection;
        if (mcpServiceCollection != null)
        {
            await mcpServiceCollection.EnsureAsync();
        }
    }
}