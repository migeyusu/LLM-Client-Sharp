using System.Windows.Input;
using LLMClient.Abstraction;

namespace LLMClient.UI;

public abstract class ModelSelectionViewModel : BaseViewModel, IModelSelection
{
    public IEndpointService EndpointService { get; set; }

    public ILLMChatModel? SelectedModel
    {
        get => _selectedModel;
        set
        {
            if (Equals(value, _selectedModel)) return;
            _selectedModel = value;
            OnPropertyChanged();
        }
    }

    public abstract ICommand? SubmitCommand { get; }

    private ILLMChatModel? _selectedModel;

    protected ModelSelectionViewModel(IEndpointService endpointService)
    {
        EndpointService = endpointService;
    }

    public ILLMChatClient? GetClient()
    {
        return this.SelectedModel?.CreateChatClient();
    }
}