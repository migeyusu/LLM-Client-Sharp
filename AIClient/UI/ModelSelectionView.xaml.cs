using System.Windows;
using System.Windows.Controls;
using LLMClient.Endpoints.Azure.Models;

namespace LLMClient.UI;

public partial class ModelSelectionView : UserControl
{
    public ModelSelectionView()
    {
        InitializeComponent();
    }

    ModelSelectionViewModel? ViewModel
    {
        get { return DataContext as ModelSelectionViewModel; }
    }

    private void TreeView_OnSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        var newValue = e.NewValue;
        if (newValue is AzureModelInfo modelInfo)
        {
            if (ViewModel != null)
            {
                ViewModel.SelectedModelName = modelInfo.Name;
                ViewModel.SelectedEndpoint = modelInfo.Endpoint;
            }
        }
        /*else if (newValue is ILLMEndpoint endpoint)
        {
            if (ViewModel != null) ViewModel.SelectedEndpoint = endpoint;
        }*/
    }
}