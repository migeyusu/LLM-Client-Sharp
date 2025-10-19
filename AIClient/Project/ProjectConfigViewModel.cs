using System.Windows;
using System.Windows.Input;
using LLMClient.Abstraction;
using LLMClient.UI;
using MaterialDesignThemes.Wpf;
using Microsoft.Xaml.Behaviors.Core;
using MessageBox = System.Windows.MessageBox;

namespace LLMClient.Project;

public class ProjectConfigViewModel
{
    public ProjectViewModel Project { get; }

    public ILLMChatClient SelectedClient { get; set; }

    public ModelSelectionViewModel ModelSelectionViewModel { get; }

    public ICommand SubmitCommand => new ActionCommand(o =>
    {
        if (!Project.Check())
        {
            return;
        }

        if (this.SelectedClient != Project.Requester.DefaultClient)
        {
            Project.Requester.DefaultClient = this.SelectedClient;
        }


        var frameworkElement = o as FrameworkElement;
        DialogHost.CloseDialogCommand.Execute(true, frameworkElement);
    });

    public ProjectConfigViewModel(ProjectViewModel project) : base()
    {
        Project = project;
        ModelSelectionViewModel = new ModelSelectionViewModel(client => { SelectedClient = client; });
    }

    public void Initialize()
    {
        var requesterDefaultClient = this.Project.Requester.DefaultClient;
        ModelSelectionViewModel.SelectedModel = requesterDefaultClient.Model;
        
    }
}