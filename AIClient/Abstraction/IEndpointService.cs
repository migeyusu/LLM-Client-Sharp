using System.Collections.ObjectModel;
using LLMClient.Endpoints.OpenAIAPI;
using LLMClient.UI;

namespace LLMClient.Abstraction;

public interface IEndpointService
{
    IReadOnlyList<ILLMEndpoint> AvailableEndpoints { get; }

    ObservableCollection<SuggestedModel> SuggestedModels { get; }

    Task Initialize();

    ILLMEndpoint? GetEndpoint(string name)
    {
        return AvailableEndpoints.FirstOrDefault((endpoint) => endpoint.Name == name);
    }
}