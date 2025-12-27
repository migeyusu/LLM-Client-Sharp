namespace LLMClient.Project;

public class ProjectPromptTemplateViewModel
{
    
    public ProjectPromptTemplateViewModel(ProjectTaskViewModel projectTask)
    {
        Task = projectTask;
        Project = projectTask.ParentProject;
    }

    public ProjectTaskViewModel Task { get; }

    public ProjectViewModel Project { get; }
}