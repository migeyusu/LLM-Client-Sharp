using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
using System.Windows.Media;
using LLMClient.Abstraction;
using LLMClient.Component.CustomControl;

namespace LLMClient.Endpoints;

/// <summary>
/// used for defining the provider and model information
/// </summary>
public class ProviderDescriptor
{
    public required string ProviderName { get; set; }
    public required string[] ModelNames { get; set; }
}

public class ProviderEntry
{
    public required string ProviderName { get; set; }

    public ThemedIcon? Icon { get; set; }
    public ObservableCollection<ModelEntry> ModelEntries { get; set; } = [];
}

public class ModelEntry
{
    public required string ModelName { get; set; }

    public ThemedIcon? Icon { get; set; }

    /// <summary>
    /// associated models in endpoints.
    /// </summary>
    public ObservableCollection<IEndpointModel> InstanceList { get; set; } = [];
}