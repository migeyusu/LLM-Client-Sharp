namespace LLMClient.Abstraction;

public interface IEndpointService
{
    IReadOnlyList<ILLMAPIEndpoint> AvailableEndpoints { get; }

    /// <summary>
    /// all available endpoint candidates, including history models and suggested endpoints
    /// </summary>
    IReadOnlyList<ILLMAPIEndpoint> CandidateEndpoints { get; }

    /// <summary>
    /// models used in history
    /// </summary>
    IReadOnlyList<ILLMChatModel> HistoryModels { get; }

    IReadOnlyList<ILLMChatModel> SuggestedModels { get; }
    
    void AddModelFrequency(ILLMChatModel model);
    
    Task Initialize();

    ILLMAPIEndpoint? GetEndpoint(string name)
    {
        return AvailableEndpoints.FirstOrDefault((endpoint) => endpoint.Name == name);
    }
}