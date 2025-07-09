using LLMClient.Abstraction;

namespace LLMClient.UI;

public class ModelSelectionViewModel : BaseViewModel
{
    public IEndpointService EndpointService { get; set; }

    public ILLMModel? SelectedModel
    {
        get => _selectedModel;
        set
        {
            if (Equals(value, _selectedModel)) return;
            _selectedModel = value;
            OnPropertyChanged();
        }
    }

    private ILLMModel? _selectedModel;

    public ModelSelectionViewModel(IEndpointService endpointService)
    {
        EndpointService = endpointService;
    }

    public ILLMClient? GetClient()
    {
        return this.SelectedModel?.CreateClient();
    }
}