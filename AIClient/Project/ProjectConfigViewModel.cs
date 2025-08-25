using System.Windows;
using System.Windows.Input;
using LLMClient.Abstraction;
using LLMClient.UI;
using MaterialDesignThemes.Wpf;
using Microsoft.Xaml.Behaviors.Core;
using MessageBox = System.Windows.Forms.MessageBox;

namespace LLMClient.Project;

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

        if (this.SelectedModel != Project.Requester.DefaultClient.Model)
        {
            var client = this.GetClient();
            if (client == null)
            {
                MessageBox.Show("create model failed!");
                return;
            }

            Project.Requester.DefaultClient = client;
        }

        if (!Project.Check())
        {
            return;
        }

        var frameworkElement = o as FrameworkElement;
        DialogHost.CloseDialogCommand.Execute(true, frameworkElement);
    });

    public ProjectConfigViewModel(ProjectViewModel project) : base()
    {
        Project = project;
    }

    public void Initialize()
    {
        SelectedModel = this.Project.Requester.DefaultClient.Model;
    }
}