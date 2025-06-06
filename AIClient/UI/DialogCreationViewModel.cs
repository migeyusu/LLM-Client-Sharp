using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using LLMClient.Abstraction;
using MaterialDesignThemes.Wpf;
using Microsoft.Xaml.Behaviors.Core;

namespace LLMClient.UI;

public class DialogCreationViewModel : ModelSelectionViewModel
{
    private string _dialogName = "新建会话";

    public DialogCreationViewModel(IEndpointService service) : base(service)
    {
        
    }

    public string DialogName
    {
        get => _dialogName;
        set
        {
            if (value == _dialogName) return;
            _dialogName = value;
            OnPropertyChanged();
        }
    }
    
    public ICommand AcceptModelDialogCommand => new ActionCommand((o =>
    {
        if (SelectedModelName == null || SelectedEndpoint == null)
        {
            MessageBox.Show("Please select model and endpoint");
            return;
        }

        var model = SelectedEndpoint.NewClient(SelectedModelName);
        if (model == null)
        {
            MessageBox.Show("create model failed!");
            return;
        }


        var frameworkElement = o as FrameworkElement;
        DialogHost.CloseDialogCommand.Execute(true, frameworkElement);
    }));
}