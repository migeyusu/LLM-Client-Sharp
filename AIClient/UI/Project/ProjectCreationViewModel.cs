using System.Windows;
using System.Windows.Input;
using LLMClient.Abstraction;
using MaterialDesignThemes.Wpf;
using Microsoft.Xaml.Behaviors.Core;
using MessageBox = System.Windows.Forms.MessageBox;

namespace LLMClient.UI.Project;

public class ProjectCreationViewModel : ModelSelectionViewModel
{
    public ProjectViewModel Project { get; } = new ProjectViewModel();

    public ICommand SubmitCommand => new ActionCommand(o =>
    {
        if (this.SelectedModel == null)
        {
            MessageBox.Show("Please select model.");
            return;
        }

        var client = this.GetClient();
        if (client == null)
        {
            MessageBox.Show("create model failed!");
            return;
        }

        Project.Client = client;
        if (!Project.Validate())
        {
            return;
        }

        var frameworkElement = o as FrameworkElement;
        DialogHost.CloseDialogCommand.Execute(true, frameworkElement);
    });

    public ProjectCreationViewModel(IEndpointService endpointService) : base(endpointService)
    {
    }
}