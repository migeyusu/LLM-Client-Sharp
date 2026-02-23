using LLMClient.Abstraction;
using LLMClient.Component.ViewModel.Base;
using LLMClient.Endpoints;

namespace LLMClient.Workflow.Research;

public abstract class ResearchClient : BaseViewModel, IChatEndpoint
{
    public abstract string Name { get; }

    private bool _isResponding;

    public bool IsResponding
    {
        get => _isResponding;
        set
        {
            if (value == _isResponding) return;
            _isResponding = value;
            OnPropertyChanged();
        }
    }

    public abstract Task<CompletedResult> SendRequest(DialogContext context, IInvokeInteractor? stream = null,
        CancellationToken cancellationToken = default);
}