using System.Collections.ObjectModel;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using LLMClient.Endpoints;
using LLMClient.Endpoints.Azure;
using LLMClient.Endpoints.OpenAIAPI;
using Microsoft.Xaml.Behaviors.Core;

namespace LLMClient.UI;

public interface IEndpointService
{
    IReadOnlyList<ILLMEndpoint> AvailableEndpoints { get; }

    Task Initialize();
}

/// <summary>
/// 作为终结点的配置中心
/// </summary>
public class EndpointConfigureViewModel : BaseViewModel, IEndpointService
{
    private readonly GithubCopilotEndPoint _githubCopilotEndPoint;

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

    public ICommand AddNewEndpointCommand => new ActionCommand((o => { Endpoints.Add(new APIEndPoint()); }));

    public ICommand RemoveEndPointCommand => new RelayCommand<ILLMEndpoint?>((o =>
    {
        if (o is APIEndPoint endpoint)
        {
            if (MessageBox.Show("Sure to remove the endpoint?" + endpoint.Name, "warning", MessageBoxButton.YesNo)
                != MessageBoxResult.Yes)
            {
                return;
            }

            Endpoints.Remove(endpoint);
        }
    }), (endpoint => { return endpoint is not GithubCopilotEndPoint; }));

    public ICommand SaveAllCommand => new ActionCommand(async o =>
    {
        var doc = await EndPointsConfiguration.LoadDoc();
        //todo:
        /*var endPointsObject = new JsonObject();
        foreach (var endpoint in Endpoints)
        {
            endpoint.UpdateConfig(endPointsObject);
        }

        doc[EndPointsConfiguration.EndpointsNodeName] = endPointsObject;
        await EndPointsConfiguration.SaveDoc(doc);*/
    });

    public ICommand ReloadSelectedCommand => new ActionCommand((async o =>
    {
        if (SelectedEndpoint != null)
        {
            var loadEndpointsNode = await EndPointsConfiguration.LoadEndpointsNode();
            SelectedEndpoint.ReloadConfig(loadEndpointsNode);
        }
    }));

    public ObservableCollection<ILLMEndpoint> Endpoints { get; set; } = new ObservableCollection<ILLMEndpoint>();

    public IReadOnlyList<ILLMEndpoint> AvailableEndpoints
    {
        get { return Endpoints.AsReadOnly(); }
    }

    private ILLMEndpoint? _selectedEndpoint;

    public EndpointConfigureViewModel(GithubCopilotEndPoint githubOption)
    {
        _githubCopilotEndPoint = githubOption;
    }

    public async Task Initialize()
    {
        var doc = await EndPointsConfiguration.LoadDoc();
        var endPointsNode = doc.GetOrCreate(EndPointsConfiguration.EndpointsNodeName);
        _githubCopilotEndPoint.ReloadConfig(endPointsNode);
        Endpoints.Add(_githubCopilotEndPoint);
        var apisNode = endPointsNode.GetOrCreate(APIEndPoint.KeyName);
        var jsonArray = apisNode.AsArray();
        foreach (var jsonNode in jsonArray)
        {
            if (jsonNode is JsonObject jsonObject)
            {
                var apiEndPoint = new APIEndPoint();
                apiEndPoint.UpdateConfig(jsonObject);
                Endpoints.Add(apiEndPoint);
            }
        }

        foreach (var availableEndpoint in Endpoints)
        {
            await availableEndpoint.InitializeAsync();
        }
    }
}