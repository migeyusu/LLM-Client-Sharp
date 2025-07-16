using System.Windows.Input;
using LLMClient.Abstraction;

namespace LLMClient.UI;

public interface IModelSelection
{
    IEndpointService EndpointService { get; }

    ILLMModel? SelectedModel { get; set; }

    ICommand? SubmitCommand { get; }
}

public abstract class ModelSelectionViewModel : BaseViewModel, IModelSelection
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

    public abstract ICommand? SubmitCommand { get; }

    private ILLMModel? _selectedModel;

    protected ModelSelectionViewModel(IEndpointService endpointService)
    {
        EndpointService = endpointService;
    }

    public ILLMClient? GetClient()
    {
        return this.SelectedModel?.CreateClient();
    }
}