using System.Windows;
using System.Windows.Input;
using LLMClient.Abstraction;
using MaterialDesignThemes.Wpf;
using Microsoft.Xaml.Behaviors.Core;

namespace LLMClient.UI.Dialog;

public class ModelSelectionPopupViewModel : ModelSelectionViewModel
{
    public RoutedCommand? SuccessRoutedCommand { get; set; } = DialogHost.CloseDialogCommand;

    public Action<ModelSelectionViewModel>? SuccessAction { get; }

    public ModelSelectionPopupViewModel(IEndpointService service, Action<ModelSelectionViewModel>? successAction = null)
        : base(service)
    {
        SuccessAction = successAction;
    }

    public override ICommand SubmitCommand => new ActionCommand((o =>
    {
        if (SelectedModel == null)
        {
            MessageBox.Show("Please select model.");
            return;
        }

        SuccessAction?.Invoke(this);
        var frameworkElement = o as FrameworkElement;
        SuccessRoutedCommand?.Execute(true, frameworkElement);
    }));
    
}