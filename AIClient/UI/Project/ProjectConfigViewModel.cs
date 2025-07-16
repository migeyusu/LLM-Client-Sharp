using System.Windows;
using System.Windows.Input;
using LLMClient.Abstraction;
using MaterialDesignThemes.Wpf;
using Microsoft.Xaml.Behaviors.Core;
using MessageBox = System.Windows.Forms.MessageBox;

namespace LLMClient.UI.Project;

public class ProjectConfigViewModel : ModelSelectionViewModel
{
    public ProjectViewModel Project { get; }

    public override ICommand SubmitCommand => new ActionCommand(o =>
    {
        if (this.SelectedModel == null)
        {
            MessageBox.Show("Please select model.");
            return;
        }

        if (this.SelectedModel != Project.Client.Model)
        {
            var client = this.GetClient();
            if (client == null)
            {
                MessageBox.Show("create model failed!");
                return;
            }

            Project.Client = client;
        }

        if (!Project.Validate())
        {
            return;
        }

        var frameworkElement = o as FrameworkElement;
        DialogHost.CloseDialogCommand.Execute(true, frameworkElement);
    });

    public ProjectConfigViewModel(IEndpointService endpointService,
        ProjectViewModel project) : base(endpointService)
    {
        Project = project;
    }

    public void Initialize()
    {
        SelectedModel = this.Project.Client.Model;
    }
}