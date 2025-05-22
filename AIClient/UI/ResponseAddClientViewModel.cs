using System.Windows;
using System.Windows.Input;
using MaterialDesignThemes.Wpf;
using Microsoft.Xaml.Behaviors.Core;

namespace LLMClient.UI;

public class ResponseAddClientViewModel : ModelSelectionViewModel
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


        if (o is FrameworkElement frameworkElement)
        {
            PopupBox.ClosePopupCommand.Execute(this, frameworkElement);
        }

        var llmModelClient = this.GetClient();
        if (llmModelClient == null)
        {
            return;
        }

        await DialogViewModel.AppendResponseOn(Response, llmModelClient);
    }));

    public ResponseAddClientViewModel(MultiResponseViewItem view,
        DialogViewModel dialogViewModel) : base(dialogViewModel.EndpointService.AvailableEndpoints)
    {
        this.Response = view;
        DialogViewModel = dialogViewModel;
    }
}