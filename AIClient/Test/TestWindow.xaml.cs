using System.Collections.ObjectModel;
using System.Windows;
using LLMClient.UI.Project;

namespace LLMClient.Test;

public partial class TestWindow : Window
{
    public TestWindow()
    {
        var projectViewModel = new ProjectViewModel
        {
            Tasks =
            [
                new ProjectTask()
                {
                    Name = "Code Generation",
                    SystemPrompt = "请描述任务的内容和目标。",
                    Type = ProjectTaskType.BugFix,
                    Status = ProjectTaskStatus.Completed
                },

                new ProjectTask()
                {
                    Name = "Code Generation",
                    SystemPrompt = "请描述任务的内容和目标。",
                    Type = ProjectTaskType.Translation,
                    Status = ProjectTaskStatus.InProgress
                }
            ]
        };
        this.DataContext = projectViewModel;
        InitializeComponent();
    }
}