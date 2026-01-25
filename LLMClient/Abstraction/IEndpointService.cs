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
    IReadOnlyList<IEndpointModel> HistoryModels { get; }

    IReadOnlyList<IEndpointModel> SuggestedModels { get; }

    void SetModelHistory(IEndpointModel model);

    Task Initialize();

    ILLMAPIEndpoint? GetEndpoint(string name)
    {
        return AvailableEndpoints.FirstOrDefault((endpoint) => endpoint.Name == name);
    }

    /// <summary>
    /// Save usage activities
    /// </summary>
    /// <returns></returns>
    Task SaveActivities();
}