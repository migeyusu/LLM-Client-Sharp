using System.Windows;
using System.Windows.Controls;

namespace LLMClient;

public partial class CreateModelView : UserControl
{
    public CreateModelView()
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
        if (newValue is string modelName)
        {
            if (ViewModel != null) ViewModel.SelectedModelId = modelName;
        }
        else if (newValue is ILLMEndpoint endpoint)
        {
            if (ViewModel != null) ViewModel.SelectedEndpoint = endpoint;
        }
    }
}