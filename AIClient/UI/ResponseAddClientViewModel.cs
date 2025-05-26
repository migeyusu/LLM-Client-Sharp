using System.Windows;
using System.Windows.Input;
using LLMClient.Abstraction;
using MaterialDesignThemes.Wpf;
using Microsoft.Xaml.Behaviors.Core;

namespace LLMClient.UI;

public class ResponseAppendClientViewModel : ModelSelectionViewModel
{
    public MultiResponseViewItem Response { get; }

    public DialogViewModel DialogViewModel { get; }

    public ICommand AcceptModelCommand => new ActionCommand((async o =>
    {
        if (this.SelectedEndpoint == null)
        {
            return;
        }

        if (SelectedModelName == null)
        {
            return;
        }

        var llmModelClient = this.GetClient();
        if (llmModelClient == null)
        {
            return;
        }
        OnModelSelected?.Invoke(llmModelClient);
        if (o is FrameworkElement frameworkElement)
        {
            PopupBox.ClosePopupCommand.Execute(this, frameworkElement);
        }
    }));

    public Action<ILLMModelClient>? OnModelSelected;

    public ResponseAppendClientViewModel(MultiResponseViewItem view,
        DialogViewModel dialogViewModel, IEndpointService endpointService, Action<ILLMModelClient>? onModelSelected)
        : base(endpointService.AvailableEndpoints)
    {
        this.Response = view;
        DialogViewModel = dialogViewModel;
        OnModelSelected = onModelSelected;
    }
}