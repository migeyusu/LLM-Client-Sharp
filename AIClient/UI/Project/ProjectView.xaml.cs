using System.Windows.Controls;
using System.Windows.Input;

namespace LLMClient.UI.Project;

public partial class ProjectView : UserControl
{
    public ProjectView()
    {
        InitializeComponent();
    }

    ProjectViewModel ViewModel => (DataContext as ProjectViewModel)!;

    private void TaskMoveLeft_OnExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        if (e.Parameter is ProjectTask task)
        {
            var i = ViewModel.Tasks.IndexOf(task);
            if (i > 0)
            {
                ViewModel.Tasks.Move(i, i - 1);
            }
        }
    }

    private void TaskDelete_OnExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        if (e.Parameter is ProjectTask task)
        {
            ViewModel.DeleteTask(task);
        }
    }
}