using System.Windows.Controls;
using System.Windows.Input;

namespace LLMClient.UI.MCP;

public partial class MCPServiceCollectionView : UserControl
{
    public MCPServiceCollectionView()
    {
        InitializeComponent();
    }

    private void DeleteItem_OnExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        var mcpServiceCollection = this.DataContext as McpServiceCollection;
        mcpServiceCollection?.DeleteServerItem(e.Parameter as McpServerItem);
    }
}