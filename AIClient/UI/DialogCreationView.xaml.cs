using System.Windows;
using System.Windows.Controls;
using LLMClient.Endpoints.Azure.Models;
using LLMClient.Endpoints.OpenAIAPI;

namespace LLMClient.UI;

public partial class DialogCreationView : UserControl
{
    public DialogCreationView()
    {
        InitializeComponent();
    }

    DialogCreationViewModel ViewModel
    {
        get { return (DataContext as DialogCreationViewModel)!; }
    }

    private void TreeView_OnSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        var newValue = e.NewValue;
        if (newValue is AzureModelInfo modelInfo)
        {
            ViewModel.SelectedModelName = modelInfo.FriendlyName;
            ViewModel.SelectedEndpoint = modelInfo.Endpoint;
        }
        else if (newValue is APIModelInfo apiModelInfo)
        {
            ViewModel.SelectedModelName = apiModelInfo.Name;
            ViewModel.SelectedEndpoint = apiModelInfo.Endpoint;
        }
        /*else if (newValue is ILLMEndpoint endpoint)
        {
            if (ViewModel != null) ViewModel.SelectedEndpoint = endpoint;
        }*/
    }
}