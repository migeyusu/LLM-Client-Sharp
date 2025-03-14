﻿using System.Collections.ObjectModel;
using System.Text.Json;
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
        var root = new JsonObject();
        var endPointsNode = root.GetOrCreate(EndPointsConfiguration.EndpointsNodeName);
        _githubCopilotEndPoint.UpdateConfig(endPointsNode);
        JsonArray jArray = new JsonArray();
        foreach (var apiEndPoint in Endpoints.Where((endpoint) => endpoint is APIEndPoint).Cast<APIEndPoint>())
        {
            var serialize = JsonSerializer.SerializeToNode(apiEndPoint);
            jArray.Add(serialize);
        }

        endPointsNode[APIEndPoint.KeyName] = jArray;
        await EndPointsConfiguration.SaveDoc(root);
        /*var endPointsObject = new JsonObject();
        foreach (var endpoint in Endpoints)
        {
            endpoint.UpdateConfig(endPointsObject);
        }

        doc[EndPointsConfiguration.EndpointsNodeName] = endPointsObject;
        await EndPointsConfiguration.SaveDoc(doc);*/
    });

    public ICommand ReloadCommand => new ActionCommand((async o =>
    {
        Endpoints.Clear();
        await this.Initialize();
    }));

    public ObservableCollection<ILLMEndpoint> Endpoints { get; set; } = new ObservableCollection<ILLMEndpoint>();

    public IReadOnlyList<ILLMEndpoint> AvailableEndpoints
    {
        get { return Endpoints.AsReadOnly(); }
    }

    private ILLMEndpoint? _selectedEndpoint;

    private readonly IServiceProvider _serviceProvider;

    public EndpointConfigureViewModel(GithubCopilotEndPoint githubOption, IServiceProvider serviceProvider)
    {
        _githubCopilotEndPoint = githubOption;
        _serviceProvider = serviceProvider;
    }

    public async Task Initialize()
    {
        var endPointsNode = await EndPointsConfiguration.LoadEndpointsNode();
        _githubCopilotEndPoint.LoadConfig(endPointsNode);
        Endpoints.Add(_githubCopilotEndPoint);
        if (endPointsNode.AsObject().TryGetPropertyValue(APIEndPoint.KeyName, out var apisNode))
        {
            var jsonArray = apisNode!.AsArray();
            foreach (var jsonNode in jsonArray)
            {
                if (jsonNode is JsonObject jsonObject)
                {
                    var endPoint = jsonObject.Deserialize<APIEndPoint>();
                    if (endPoint != null)
                    {
                        Endpoints.Add(endPoint);
                    }
                }
            }
        }


        foreach (var availableEndpoint in Endpoints)
        {
            await availableEndpoint.InitializeAsync();
        }
    }
}