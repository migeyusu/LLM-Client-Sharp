using LLMClient.Abstraction;
using LLMClient.Agent;
using LLMClient.Component.ViewModel.Base;
using LLMClient.Dialog.Models;
using LLMClient.Endpoints;

namespace LLMClient.Workflow.Research;

public abstract class ResearchClient : BaseViewModel, IAgent
{
    public abstract string Name { get; }

    public bool IsResponding
    {
        get;
        set
        {
            if (value == field) return;
            field = value;
            OnPropertyChanged();
        }
    }

    public abstract IAsyncEnumerable<ChatCallResult> Execute(ITextDialogSession dialogSession,
        IInvokeInteractor? interactor = null,
        CancellationToken cancellationToken = default);

    public Task Start()
    {
        throw new NotImplementedException();
    }
}