using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Input;
using LLMClient.Azure;
using MaterialDesignThemes.Wpf;
using Microsoft.Xaml.Behaviors.Core;

namespace LLMClient.UI;

public interface IEndpointService
{
    IList<ILLMEndpoint> AvailableEndpoints { get; }

    Task Initialize();
}

public class EndpointConfigureViewModel : BaseViewModel, IEndpointService
{
    public ILLMEndpoint? SelectedEndpoint
    {
        get => _selectedEndpoint;
        set
        {
            if (Equals(value, _selectedEndpoint)) return;
            _selectedEndpoint = value;
            OnPropertyChanged();
        }
    }

    public ICommand? SaveCommand => new ActionCommand(async o =>
    {
        if (SelectedEndpoint != null)
        {
            var node = await LoadEndpointsNode();
            SelectedEndpoint.UpdateConfig(node);
            await SaveEndpoints(node);
        }
    });

    public ICommand? ReloadCommand => new ActionCommand((async o =>
    {
        if (SelectedEndpoint != null)
        {
            var loadEndpointsNode = await LoadEndpointsNode();
            SelectedEndpoint.ReloadConfig(loadEndpointsNode);
        }
    }));

    public IList<ILLMEndpoint> AvailableEndpoints { get; set; } = new List<ILLMEndpoint>();

    private ILLMEndpoint? _selectedEndpoint;

    public EndpointConfigureViewModel(AzureEndPoint azureOption)
    {
        AvailableEndpoints.Add(azureOption);
    }

    private const string EndPointsJsonFileName = "EndPoints.json";

    private async Task<JsonNode> LoadEndpointsNode()
    {
        var fullPath = Path.GetFullPath(EndPointsJsonFileName);
        var fileInfo = new FileInfo(fullPath);
        if (fileInfo.Exists)
        {
            await using (var fileStream = fileInfo.OpenRead())
            {
                var jsonNode = await JsonNode.ParseAsync(fileStream);
                if (jsonNode != null)
                {
                    return jsonNode;
                }
            }
        }

        return JsonNode.Parse("""{}""")!;
    }

    private async Task SaveEndpoints(JsonNode node)
    {
        var fullPath = Path.GetFullPath(EndPointsJsonFileName);
        var fileInfo = new FileInfo(fullPath);
        fileInfo.Directory?.Create();
        if (fileInfo.Exists)
        {
            fileInfo.Delete();
        }

        await using (var fileStream = fileInfo.OpenWrite())
        {
            await using (var utf8JsonWriter = new Utf8JsonWriter(fileStream))
            {
                node.WriteTo(utf8JsonWriter);
            }
        }
    }

    public async Task Initialize()
    {
        foreach (var availableEndpoint in AvailableEndpoints)
        {
            await availableEndpoint.InitializeAsync();
        }

        var node = await LoadEndpointsNode();
        foreach (var availableEndpoint in AvailableEndpoints)
        {
            availableEndpoint.ReloadConfig(node);
        }
    }
}