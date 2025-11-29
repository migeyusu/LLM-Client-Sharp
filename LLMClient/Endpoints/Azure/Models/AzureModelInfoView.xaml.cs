using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace LLMClient.Endpoints.Azure.Models;

public partial class AzureModelInfoView : UserControl
{
    public AzureModelInfoView()
    {
        InitializeComponent();
    }

    private void FrameworkElement_OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (sender is FlowDocumentScrollViewer oldOwner)
        {
            oldOwner.Document = new FlowDocument();
        }
    }
}