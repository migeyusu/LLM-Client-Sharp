using System.Collections.ObjectModel;
using LLMClient.Endpoints.OpenAIAPI;

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

public class SuggestedModel
{
    public SuggestedModel(ILLMEndpoint endpoint, ILLMModel llmModel)
    {
        Endpoint = endpoint;
        LlmModel = llmModel;
    }

    public ILLMEndpoint Endpoint { get; set; }

    public ILLMModel LlmModel { get; set; }
}