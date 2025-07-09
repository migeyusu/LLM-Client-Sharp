using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using LLMClient.Abstraction;
using LLMClient.Endpoints;
using LLMClient.Endpoints.Azure;
using LLMClient.Endpoints.OpenAIAPI;
using LLMClient.UI.Component;
using Microsoft.Xaml.Behaviors.Core;

namespace LLMClient.UI;

/// <summary>
/// 作为终结点的配置中心
/// </summary>
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

    public ICommand AddNewEndpointCommand => new ActionCommand((o => { Endpoints.Add(new APIEndPoint()); }));

    public ICommand RemoveEndPointCommand => new RelayCommand<ILLMEndpoint?>((o =>
    {
        if (o is APIEndPoint endpoint)
        {
            if (MessageBox.Show($"Sure to remove the endpoint {endpoint.DisplayName} ?", "warning",
                    MessageBoxButton.YesNo)
                != MessageBoxResult.Yes)
            {
                return;
            }

            Endpoints.Remove(endpoint);
        }
    }));

    public ICommand SaveAllCommand => new ActionCommand(async o =>
    {
        try
        {
            var distinctEndpoints = Endpoints.DistinctBy((endpoint => endpoint.Name));
            var difference = distinctEndpoints.Except(Endpoints).ToList();
            if (difference.Count != 0)
            {
                difference.ForEach(endpoint => { MessageBox.Show("Endpoint name must be unique:" + endpoint.Name); });
                return;
            }

            var root = new JsonObject();
            var endPointsNode = root.GetOrCreate(EndPointsConfiguration.EndpointsNodeName);
            _githubCopilotEndPoint?.UpdateConfig(endPointsNode);
            // 1. 拿到所有的 APIEndPoint
            var apiEndpoints = Endpoints
                .OfType<APIEndPoint>()
                .ToList();
            foreach (var ep in apiEndpoints)
            {
                if (!ep.Validate(out var msg))
                {
                    MessageBox.Show(msg);
                    return;
                }
            }

            endPointsNode[APIEndPoint.KeyName] = JsonSerializer.SerializeToNode(apiEndpoints);
            var keyValuePairs = this.SuggestedModels
                .Select(model => new KeyValuePair<string, string>(model.LlmModel.Endpoint.Name, model.LlmModel.Name))
                .ToArray();
            endPointsNode[SuggestedModelKey] = JsonSerializer.SerializeToNode(keyValuePairs);
            //todo: 添加template 保存
            await EndPointsConfiguration.SaveDoc(root);
            MessageEventBus.Publish("已保存！");
        }
        catch (Exception e)
        {
            MessageBox.Show("保存失败: " + e.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
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
        get { return Endpoints.ToArray().AsReadOnly(); }
    }

    public ObservableCollection<SuggestedModel> SuggestedModels { get; } =
        new ObservableCollection<SuggestedModel>();

    public PopupModelSelectionViewModel PopupSelectViewModel { get; }

    public ICommand RemoveSuggestedModelCommand => new ActionCommand((o =>
    {
        if (o is SuggestedModel suggestedModel)
        {
            this.SuggestedModels.Remove(suggestedModel);
        }
    }));

    private ILLMEndpoint? _selectedEndpoint;

    public EndpointConfigureViewModel()
    {
        PopupSelectViewModel = new PopupModelSelectionViewModel(this, OnModelSelected);
        Endpoints.CollectionChanged += EndpointsOnCollectionChanged;
    }

    private void EndpointsOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        this.OnPropertyChanged(nameof(AvailableEndpoints));
    }

    private void OnModelSelected(ModelSelectionViewModel obj)
    {
        if (SuggestedModels.Count > 6)
        {
            MessageEventBus.Publish("最多只能添加6个推荐模型");
            return;
        }

        var llmModel = obj.SelectedModel;
        if (llmModel is null)
            return;
        this.SuggestedModels.Add(new SuggestedModel(llmModel));
    }

    private GithubCopilotEndPoint? _githubCopilotEndPoint;

    private const string SuggestedModelKey = "SuggestedModels";

    public async Task Initialize()
    {
        var endPoints = (await EndPointsConfiguration.LoadEndpointsNode()).AsObject();
        _githubCopilotEndPoint = GithubCopilotEndPoint.TryLoad(endPoints);
        Endpoints.Add(_githubCopilotEndPoint);
        if (endPoints.TryGetPropertyValue(APIEndPoint.KeyName, out var apisNode))
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

        if (endPoints.TryGetPropertyValue(SuggestedModelKey, out var suggestedModelsNode))
        {
            var keyValuePair = suggestedModelsNode.Deserialize<KeyValuePair<string, String>[]>();
            if (keyValuePair != null)
            {
                foreach (var valuePair in keyValuePair)
                {
                    var valuePairKey = valuePair.Key;
                    var llmEndpoint = ((IEndpointService)this).GetEndpoint(valuePairKey);
                    if (llmEndpoint != null)
                    {
                        var llmModel = llmEndpoint.GetModel(valuePair.Value);
                        if (llmModel != null)
                        {
                            SuggestedModels.Add(new SuggestedModel(llmModel));
                        }
                    }
                }
            }
        }
    }
}