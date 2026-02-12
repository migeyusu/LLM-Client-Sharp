namespace LLMClient.Abstraction;

public interface IEndpointService
{
    /// <summary>
    /// distinct endpoints, which can be used for selection when creating a new session. It may contain endpoints that are not used in history, but can be used for new sessions. It should not contain endpoints that are used in history but not available anymore.
    /// </summary>
    IReadOnlyList<ILLMAPIEndpoint> AvailableEndpoints { get; }

    /// <summary>
    /// for ui, including history models and suggested endpoints
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