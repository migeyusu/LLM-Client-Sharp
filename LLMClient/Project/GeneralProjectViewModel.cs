using AutoMapper;
using LLMClient.Abstraction;
using LLMClient.Component.ViewModel;
using LLMClient.Configuration;

namespace LLMClient.Project;

public class GeneralProjectViewModel : ProjectViewModel
{
    public GeneralProjectViewModel(ProjectOption projectOption, ILLMChatClient modelClient, IMapper mapper,
        GlobalOptions options, IViewModelFactory factory, IEnumerable<ProjectTaskViewModel>? tasks = null) :
        base(projectOption, modelClient, mapper, options, factory, tasks)
    {
    }
}