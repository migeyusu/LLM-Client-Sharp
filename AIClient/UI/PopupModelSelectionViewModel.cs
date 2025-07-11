using System.Windows;
using System.Windows.Input;
using LLMClient.Abstraction;
using MaterialDesignThemes.Wpf;
using Microsoft.Xaml.Behaviors.Core;

namespace LLMClient.UI;

public class PopupModelSelectionViewModel : ModelSelectionViewModel
{
    public ICommand AcceptModelCommand => new ActionCommand(o =>
    {
        if (SelectedModel == null)
        {
            return;
        }

        OnModelSelected?.Invoke(this);
        if (o is FrameworkElement frameworkElement)
        {
            PopupBox.ClosePopupCommand.Execute(this, frameworkElement);
        }
    });

    public Action<ModelSelectionViewModel>? OnModelSelected;

    public PopupModelSelectionViewModel(IEndpointService endpointService,
        Action<ModelSelectionViewModel>? onModelSelected)
        : base(endpointService)
    {
        OnModelSelected = onModelSelected;
    }
}