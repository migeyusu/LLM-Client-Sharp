using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Input;
using AutoMapper;
using CommunityToolkit.Mvvm.Input;
using LLMClient.Abstraction;
using LLMClient.Component.Utility;
using LLMClient.Component.ViewModel;
using LLMClient.Component.ViewModel.Base;
using LLMClient.Data;
using LLMClient.Dialog;
using LLMClient.Endpoints.Azure;
using LLMClient.Endpoints.OpenAIAPI;
using MaterialDesignThemes.Wpf;
using Microsoft.Extensions.Logging;
using Microsoft.Xaml.Behaviors.Core;
using MessageBox = System.Windows.MessageBox;

namespace LLMClient.Endpoints;

/// <summary>
/// 作为终结点的配置中心
/// </summary>
public class EndpointConfigureViewModel : BaseViewModel, IEndpointService
{
    private readonly ILoggerFactory _loggerFactory;

    public ILLMAPIEndpoint? SelectedEndpoint
    {
        get => _selectedEndpoint;
        set
        {
            if (Equals(value, _selectedEndpoint)) return;
            _selectedEndpoint = value;
            OnPropertyChanged();
        }
    }

    public ICommand AddNewEndpointCommand => new ActionCommand((o =>
    {
        Endpoints.Add(new APIEndPoint(new APIEndPointOption(), _loggerFactory));
    }));

    public ICommand RemoveEndPointCommand => new RelayCommand<ILLMAPIEndpoint?>((o =>
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

    public ICommand SaveAllCommand => new ActionCommand(async void (_) =>
    {
        try
        {
            var distinctEndpoints = Endpoints.DistinctBy(endpoint => endpoint.Name);
            var difference = distinctEndpoints.Except(Endpoints).ToList();
            if (difference.Count != 0)
            {
                difference.ForEach(endpoint => { MessageBox.Show("Endpoint name must be unique:" + endpoint.Name); });
                return;
            }

            var doc = await EndPointsConfig.LoadOrCreateRoot();
            var root = doc.AsObject();
            var endPointsNode = root.GetOrCreate(EndPointsConfig.EndpointsNodeName);
            _githubCopilotEndPoint?.UpdateConfig(endPointsNode);
            // 1. 拿到所有的 APIEndPoint
            var apiEndpoints = Endpoints
                .OfType<APIEndPoint>()
                .Select(endPoint => endPoint.Option)
                .ToArray();
            foreach (var ep in apiEndpoints)
            {
                if (!ep.Validate(out var msg))
                {
                    MessageBox.Show("保存失败: " + msg, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }

            var options = Extension.DefaultJsonSerializerOptions;
            endPointsNode[APIEndPoint.KeyName] = JsonSerializer.SerializeToNode(apiEndpoints, options);
            var keyValuePairs = this.SuggestedModels.Select((model => new LLMModelPersistModel()
            {
                ModelName = model.Name,
                EndPointName = model.Endpoint.Name,
            })).ToArray();
            var serializeToNode = JsonSerializer.SerializeToNode(keyValuePairs, options);
            endPointsNode[SuggestedModelKey] = serializeToNode;
            /*var historyKeyValuePairs = this.HistoryModels.Select((model => new LLMModelPersistModel()
            {
                ModelName = model.Name,
                EndPointName = model.Endpoint.Name,
            })).ToArray();
            var historySerializeToNode = JsonSerializer.SerializeToNode(historyKeyValuePairs, options);
            endPointsNode[HistoryModelKey] = historySerializeToNode;
            */
            await EndPointsConfig.SaveDoc(root);
            MessageEventBus.Publish("已保存！");
        }
        catch (Exception e)
        {
            MessageBox.Show("保存失败: " + e.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    });

    public ICommand ReloadCommand => new ActionCommand((async void (o) =>
    {
        Endpoints.Clear();
        await this.Initialize();
    }));

    public ObservableCollection<ILLMAPIEndpoint> Endpoints { get; set; } = [];

    private readonly ReadOnlyObservableCollection<ILLMAPIEndpoint> _availableEndpoints;

    public IReadOnlyList<ILLMAPIEndpoint> AvailableEndpoints
    {
        get { return _availableEndpoints; }
    }

    public IReadOnlyList<ILLMAPIEndpoint> CandidateEndpoints
    {
        get
        {
            var list = new List<ILLMAPIEndpoint>(Endpoints.Count + 2)
            {
                _historyEndPoint,
                _suggestedEndPoint
            };
            list.AddRange(Endpoints);
            return list;
        }
    }

    private readonly StubEndPoint _historyEndPoint;

    private readonly StubEndPoint _suggestedEndPoint;

    private readonly ObservableCollection<IEndpointModel> _historyChatModelsOb = [];

    private readonly ReadOnlyObservableCollection<IEndpointModel> _historyChatModels;

    public IReadOnlyList<IEndpointModel> HistoryModels
    {
        get { return _historyChatModels; }
    }

    private readonly ReadOnlyObservableCollection<IEndpointModel> _suggestedModels;

    public IReadOnlyList<IEndpointModel> SuggestedModels
    {
        get { return _suggestedModels; }
    }

    public void SetModelHistory(IEndpointModel model)
    {
        var indexOf = _historyChatModelsOb.IndexOf(model);
        if (indexOf >= 0)
        {
            _historyChatModelsOb.Move(indexOf, 0);
        }
        else
        {
            _historyChatModelsOb.Insert(0, model);
            if (_historyChatModelsOb.Count > 20)
            {
                _historyChatModelsOb.RemoveAt(_historyChatModelsOb.Count - 1);
            }
        }
    }

    public ObservableCollection<IEndpointModel> SuggestedModelsOb { get; } =
        new ObservableCollection<IEndpointModel>();

    public ModelSelectionPopupViewModel PopupSelectViewModel { get; }

    public ICommand RemoveSuggestedModelCommand => new ActionCommand((o =>
    {
        if (o is IEndpointModel suggestedModel)
        {
            this.SuggestedModelsOb.Remove(suggestedModel);
        }
    }));

    private ILLMAPIEndpoint? _selectedEndpoint;

    public EndpointConfigureViewModel(ILoggerFactory loggerFactory, IMapper mapper)
    {
        this._loggerFactory = loggerFactory;
        PopupSelectViewModel = new ModelSelectionPopupViewModel(OnModelSelected)
            { SuccessRoutedCommand = PopupBox.ClosePopupCommand };
        _suggestedModels = new ReadOnlyObservableCollection<IEndpointModel>(SuggestedModelsOb);
        _availableEndpoints = new ReadOnlyObservableCollection<ILLMAPIEndpoint>(Endpoints);
        _historyChatModels = new ReadOnlyObservableCollection<IEndpointModel>(_historyChatModelsOb);
        _historyEndPoint = new StubEndPoint(_historyChatModelsOb)
        {
            DisplayName = "History Models",
            Name = "HistoryEndPoint",
            Icon = PackIconKind.History.GetThemedIcon(),
        };
        _suggestedEndPoint = new StubEndPoint(SuggestedModelsOb)
        {
            DisplayName = "Suggested Models",
            Name = "SuggestedEndPoint",
            Icon = PackIconKind.StarOutline.GetThemedIcon(),
        };
        /*var collection = new CompositeCollection();
        var collectionContainer = new CollectionContainer() { Collection = Endpoints };
        collection.Add(collectionContainer);
        collection.Add(historyEndPoint);
        collection.Add(suggestedEndPoint);*/
    }

    private void OnModelSelected(BaseModelSelectionViewModel obj)
    {
        if (SuggestedModelsOb.Count > 6)
        {
            MessageEventBus.Publish("最多只能添加6个推荐模型");
            return;
        }

        this.SuggestedModelsOb.Add(obj.Model);
    }

    private GithubCopilotEndPoint? _githubCopilotEndPoint;

    private const string SuggestedModelKey = "SuggestedModels";

    private const string HistoryModelKey = "HistoryModels";

    private const string TelemetryKey = "Telemetry";

    public async Task Initialize()
    {
        var root = await EndPointsConfig.LoadOrCreateRoot();
        var endPoints = root.GetOrCreate(EndPointsConfig.EndpointsNodeName).AsObject();
        _githubCopilotEndPoint = GithubCopilotEndPoint.TryLoad(endPoints);
        Endpoints.Add(_githubCopilotEndPoint);
        if (endPoints.TryGetPropertyValue(APIEndPoint.KeyName, out var apisNode))
        {
            var jsonArray = apisNode!.AsArray();
            foreach (var jsonNode in jsonArray)
            {
                if (jsonNode is JsonObject jsonObject)
                {
                    var endPoint = jsonObject.Deserialize<APIEndPointOption>(Extension.DefaultJsonSerializerOptions);
                    if (endPoint != null)
                    {
                        Endpoints.Add(new APIEndPoint(endPoint, _loggerFactory));
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
            var modelPersistModels = suggestedModelsNode.Deserialize<LLMModelPersistModel[]>();
            if (modelPersistModels != null)
            {
                foreach (var modelPersistModel in modelPersistModels)
                {
                    var endPointName = modelPersistModel.EndPointName;
                    var llmEndpoint = ((IEndpointService)this).GetEndpoint(endPointName);
                    if (llmEndpoint != null)
                    {
                        var llmModel = llmEndpoint.GetModel(modelPersistModel.ModelName);
                        if (llmModel != null)
                        {
                            SuggestedModelsOb.Add(llmModel);
                        }
                    }
                }
            }

            OnPropertyChangedAsync(nameof(SuggestedModels));
        }

        if (endPoints.TryGetPropertyValue(HistoryModelKey, out var historyModels))
        {
            var modelPersistModels = historyModels.Deserialize<LLMModelPersistModel[]>();
            if (modelPersistModels != null)
            {
                foreach (var modelPersistModel in modelPersistModels)
                {
                    var endPointName = modelPersistModel.EndPointName;
                    var llmEndpoint = ((IEndpointService)this).GetEndpoint(endPointName);
                    if (llmEndpoint != null)
                    {
                        var llmModel = llmEndpoint.GetModel(modelPersistModel.ModelName);
                        if (llmModel != null)
                        {
                            _historyChatModelsOb.Add(llmModel);
                        }
                    }
                }
            }

            OnPropertyChangedAsync(nameof(HistoryModels));
        }

        if (root.AsObject().TryGetPropertyValue(TelemetryKey, out var telemetry))
        {
            if (telemetry != null)
            {
                var telemetryArray = telemetry.AsArray();
                foreach (var endpointNode in telemetryArray.OfType<JsonObject>())
                {
                    if (!endpointNode.TryGetPropertyValue("Name", out var nameNode))
                    {
                        continue;
                    }

                    var endpointName = nameNode?.GetValue<string>();
                    if (string.IsNullOrWhiteSpace(endpointName))
                    {
                        continue;
                    }

                    var llmEndpoint = ((IEndpointService)this).GetEndpoint(endpointName);
                    if (llmEndpoint == null)
                    {
                        continue;
                    }

                    if (!endpointNode.TryGetPropertyValue("Models", out var modelsNode))
                    {
                        continue;
                    }

                    if (modelsNode == null)
                    {
                        continue;
                    }

                    foreach (var modelNode in modelsNode.AsArray().OfType<JsonObject>())
                    {
                        if (!modelNode.TryGetPropertyValue("ModelName", out var modelNameNode))
                        {
                            continue;
                        }

                        var modelName = modelNameNode?.GetValue<string>();
                        if (string.IsNullOrWhiteSpace(modelName))
                        {
                            continue;
                        }

                        var model = llmEndpoint.GetModel(modelName);
                        if (model == null)
                        {
                            continue;
                        }

                        if (modelNode.TryGetPropertyValue("Telemetry", out var modelTelemetry))
                        {
                            model.Telemetry =
                                modelTelemetry.Deserialize<UsageCount>(Extension.DefaultJsonSerializerOptions);
                        }
                    }
                }
            }
        }
    }

    public async Task SaveActivities()
    {
        var options = Extension.DefaultJsonSerializerOptions;
        var root = await EndPointsConfig.LoadOrCreateRoot();
        // load then change history only
        var endPointsNode = root.GetOrCreate(EndPointsConfig.EndpointsNodeName);
        var historyKeyValuePairs = this.HistoryModels.Select((model => new LLMModelPersistModel()
        {
            ModelName = model.Name,
            EndPointName = model.Endpoint.Name,
        })).ToArray();
        var historySerializeToNode = JsonSerializer.SerializeToNode(historyKeyValuePairs, options);
        var newContent = JsonSerializer.Serialize(historySerializeToNode, options);
        var oldNode = endPointsNode.GetOrCreate(HistoryModelKey);
        var oldContent = JsonSerializer.Serialize(oldNode, options);
        if (newContent != oldContent)
        {
            endPointsNode[HistoryModelKey] = historySerializeToNode;
        }

        //save telemetry 
        //get all telemetry enabled endpoints
        var jsonArray = new JsonArray();
        foreach (var endpoint in this.Endpoints)
        {
            var models = new JsonArray();
            foreach (var availableModel in endpoint.AvailableModels)
            {
                if (availableModel.Telemetry != null)
                {
                    models.Add(new JsonObject()
                    {
                        ["ModelName"] = availableModel.Name,
                        ["Telemetry"] = JsonSerializer.SerializeToNode(availableModel.Telemetry,
                            Extension.DefaultJsonSerializerOptions)
                    });
                }
            }

            if (models.Count > 0)
            {
                var jsonObject = new JsonObject
                {
                    ["Name"] = endpoint.Name,
                    ["Models"] = models
                };
                jsonArray.Add(jsonObject);
            }
        }

        root[TelemetryKey] = jsonArray;
        await EndPointsConfig.SaveDoc(root);
    }
}