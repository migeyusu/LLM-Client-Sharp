using AutoMapper;
using LLMClient.Abstraction;
using LLMClient.Component.ViewModel;
using LLMClient.Configuration;

namespace LLMClient.Project;

public class CppProjectViewModel : ProjectViewModel
{
    public CppProjectViewModel(ProjectOption option, ILLMChatClient modelClient, IMapper mapper,
        GlobalOptions options, IViewModelFactory factory,
        IEnumerable<ProjectSessionViewModel>? tasks = null)
        : base(option, modelClient, mapper, options, factory, tasks)
    {
    }
}