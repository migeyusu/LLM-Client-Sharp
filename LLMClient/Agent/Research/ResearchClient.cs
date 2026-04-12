using LLMClient.Abstraction;
using LLMClient.Component.ViewModel.Base;
using LLMClient.Dialog.Models;

namespace LLMClient.Agent.Research;

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

    public abstract IAsyncEnumerable<ReactStep> Execute(ITextDialogSession dialogSession,
        CancellationToken cancellationToken = default);
}