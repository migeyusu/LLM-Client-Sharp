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
    IReadOnlyList<ILLMModel> HistoryModels { get; }

    IReadOnlyList<ILLMModel> SuggestedModels { get; }

    void SetModelHistory(ILLMModel model);

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