using System.Windows;
using System.Windows.Input;
using LLMClient.Abstraction;
using LLMClient.UI.ViewModel;
using MaterialDesignThemes.Wpf;
using Microsoft.Xaml.Behaviors.Core;

namespace LLMClient.Project;

public class ProjectConfigViewModel
{
    public ProjectViewModel Project { get; }

    public ModelSelectionViewModel ModelSelectionViewModel { get; }

    public ICommand SubmitCommand => new ActionCommand(o =>
    {
        if (!Project.Check())
        {
            return;
        }

        if (this.ModelSelectionViewModel.SelectedModel != Project.Requester.DefaultClient.Model)
        {
            var llmChatClient = this.ModelSelectionViewModel.GetClient();
            if (llmChatClient == null)
            {
                MessageBox.Show("模型客户端初始化失败", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            Project.Requester.DefaultClient = llmChatClient;
        }


        var frameworkElement = o as FrameworkElement;
        DialogHost.CloseDialogCommand.Execute(true, frameworkElement);
    });

    public ProjectConfigViewModel(ProjectViewModel project) : base()
    {
        Project = project;
        ModelSelectionViewModel = new ModelSelectionViewModel();
    }

    public void Initialize()
    {
        var requesterDefaultClient = this.Project.Requester.DefaultClient;
        ModelSelectionViewModel.SelectedModel = requesterDefaultClient.Model;
    }
}