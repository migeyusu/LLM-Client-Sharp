using LLMClient.Abstraction;

namespace LLMClient.UI;

public class ModelSelectionViewModel : BaseViewModel
{
    public IEndpointService EndpointService { get; set; }

    public string? SelectedModelName
    {
        get => _selectedModelName;
        set
        {
            if (value == _selectedModelName) return;
            _selectedModelName = value;
            OnPropertyChanged();
        }
    }

    public ILLMEndpoint? SelectedEndpoint
    {
        get => _selectedEndpoint;
        set
        {
            if (Equals(value, _selectedEndpoint)) return;
            _selectedEndpoint = value;
            OnPropertyChanged();
        }
    }


    private string? _selectedModelName;

    private ILLMEndpoint? _selectedEndpoint;

    public ModelSelectionViewModel(IEndpointService endpointService)
    {
        EndpointService = endpointService;
    }

    public ILLMClient? GetClient()
    {
        if (this.SelectedModelName == null)
        {
            return null;
        }

        var model = this.SelectedEndpoint?.NewClient(this.SelectedModelName);
        return model;
    }
}