using LLMClient.Component.ViewModel;

namespace LLMClient.Abstraction;

/// <summary>
/// Telemetry data preserved for a model/endpoint that has been deleted.
/// </summary>
public record ArchivedModelTelemetry(
    string EndpointName,
    string EndpointDisplayName,
    string ModelName,
    UsageCounter Telemetry);

public interface IEndpointService
{
    /// <summary>
    /// All registered endpoints (unfiltered, including disabled ones).
    /// </summary>
    IReadOnlyList<IAPIEndpoint> AllEndpoints { get; }

    /// <summary>
    /// For UI, including history models, suggested endpoints and enabled real endpoints (disabled ones excluded).
    /// </summary>
    IReadOnlyList<IAPIEndpoint> CandidateEndpoints { get; }

    /// <summary>
    /// models used in history
    /// </summary>
    IReadOnlyList<IEndpointModel> HistoryModels { get; }

    IReadOnlyList<IEndpointModel> SuggestedModels { get; }

    void SetModelHistory(IEndpointModel model);

    /// <summary>
    /// Telemetry data for models/endpoints that have been deleted, preserved across sessions.
    /// </summary>
    IReadOnlyList<ArchivedModelTelemetry> ArchivedTelemetry => [];

    Task Initialize();

    IAPIEndpoint? GetEndpoint(string name)
    {
        return AllEndpoints.FirstOrDefault((endpoint) => endpoint.Name == name);
    }

    /// <summary>
    /// Save usage activities
    /// </summary>
    /// <returns></returns>
    Task SaveActivities();
}