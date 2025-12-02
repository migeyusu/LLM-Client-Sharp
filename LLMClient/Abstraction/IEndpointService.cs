namespace LLMClient.Abstraction;

public interface IEndpointService
{
    IReadOnlyList<ILLMEndpoint> AvailableEndpoints { get; }

    /// <summary>
    /// all available endpoint candidates, including history models and suggested endpoints
    /// </summary>
    IReadOnlyList<ILLMEndpoint> CandidateEndpoints { get; }

    /// <summary>
    /// models used in history
    /// </summary>
    IReadOnlyList<ILLMChatModel> HistoryModels { get; }

    IReadOnlyList<ILLMChatModel> SuggestedModels { get; }
    
    void AddModelFrequency(ILLMChatModel model);
    
    Task Initialize();

    ILLMEndpoint? GetEndpoint(string name)
    {
        return AvailableEndpoints.FirstOrDefault((endpoint) => endpoint.Name == name);
    }
}