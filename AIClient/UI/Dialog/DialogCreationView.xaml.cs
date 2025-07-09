using System.Windows;
using System.Windows.Controls;
using LLMClient.Abstraction;
using LLMClient.Endpoints.Azure.Models;
using LLMClient.Endpoints.OpenAIAPI;

namespace LLMClient.UI.Dialog;

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
        if (newValue is ILLMModel modelInfo)
        {
            ViewModel.SelectedModel = modelInfo;
        }
    }
}