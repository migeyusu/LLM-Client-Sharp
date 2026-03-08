using AutoMapper;
using LLMClient.Abstraction;
using LLMClient.Component.ViewModel;
using LLMClient.Configuration;

namespace LLMClient.Project;

public class CppProjectViewModel : ProjectViewModel
{
    public CppProjectViewModel(ProjectOption option, string initialPrompt, ILLMChatClient modelClient, IMapper mapper,
        GlobalOptions options, IViewModelFactory factory,
        IEnumerable<ProjectSessionViewModel>? tasks = null)
        : base(option, initialPrompt, modelClient, mapper, options, factory, tasks)
    {
    }
}