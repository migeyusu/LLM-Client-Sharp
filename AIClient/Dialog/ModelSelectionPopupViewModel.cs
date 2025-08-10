using System.Windows;
using System.Windows.Input;
using LLMClient.Abstraction;
using LLMClient.UI;
using MaterialDesignThemes.Wpf;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Xaml.Behaviors.Core;

namespace LLMClient.Dialog;

public class ModelSelectionPopupViewModel : ModelSelectionViewModel
{
    public RoutedCommand? SuccessRoutedCommand { get; set; } = DialogHost.CloseDialogCommand;

    public Action<ModelSelectionViewModel>? SuccessAction { get; }

    public ModelSelectionPopupViewModel(IEndpointService service, Action<ModelSelectionViewModel>? successAction = null)
        : base(service)
    {
        SuccessAction = successAction;
    }

    public ModelSelectionPopupViewModel(Action<ModelSelectionViewModel>? successAction = null)
        : this(ServiceLocator.GetRequiredService<IEndpointService>(), successAction)
    {
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