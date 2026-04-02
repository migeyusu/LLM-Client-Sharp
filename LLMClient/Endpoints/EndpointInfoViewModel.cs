using LLMClient.Abstraction;
using LLMClient.Component.ViewModel.Base;

namespace LLMClient.Endpoints;

/// <summary>
/// ViewModel for the Endpoint Info dialog (left: endpoint list, right: placeholder for future content).
/// </summary>
public class EndpointInfoViewModel : BaseViewModel
{
    private ILLMAPIEndpoint? _selectedEndpoint;

    public IReadOnlyList<ILLMAPIEndpoint> Endpoints { get; }

    public ILLMAPIEndpoint? SelectedEndpoint
    {
        get => _selectedEndpoint;
        set => SetField(ref _selectedEndpoint, value);
    }

    public EndpointInfoViewModel(IEndpointService service)
    {
        Endpoints = service.AllEndpoints;
    }
}

