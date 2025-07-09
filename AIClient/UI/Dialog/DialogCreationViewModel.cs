using System.Windows;
using System.Windows.Input;
using LLMClient.Abstraction;
using MaterialDesignThemes.Wpf;
using Microsoft.Xaml.Behaviors.Core;

namespace LLMClient.UI.Dialog;

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
        if (SelectedModel == null )
        {
            MessageBox.Show("Please select model.");
            return;
        }

        var model = SelectedModel.CreateClient();
        if (model == null)
        {
            MessageBox.Show("create model failed!");
            return;
        }


        var frameworkElement = o as FrameworkElement;
        DialogHost.CloseDialogCommand.Execute(true, frameworkElement);
    }));
}