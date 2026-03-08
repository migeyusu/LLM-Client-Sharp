using AutoMapper;
using LLMClient.Abstraction;
using LLMClient.Component.ViewModel;
using LLMClient.Configuration;

namespace LLMClient.Project;

public class GeneralProjectViewModel : ProjectViewModel
{
    public GeneralProjectViewModel(ProjectOption projectOption, string initialPrompt, ILLMChatClient modelClient,
        IMapper mapper, GlobalOptions options, IViewModelFactory factory,
        IEnumerable<ProjectSessionViewModel>? tasks = null) :
        base(projectOption, initialPrompt, modelClient, mapper, options, factory, tasks)
    {
    }
}