using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Input;
using AutoMapper;
using CommunityToolkit.Mvvm.Input;
using LLMClient.Abstraction;
using LLMClient.Component.CustomControl;
using LLMClient.Component.UserControls;
using LLMClient.Component.Utility;
using LLMClient.Component.ViewModel;
using LLMClient.Component.ViewModel.Base;

using LLMClient.Dialog;
using LLMClient.Endpoints.Azure;
using LLMClient.Endpoints.OpenAIAPI;

using LLMClient.Persistence;
using MaterialDesignThemes.Wpf;
using Microsoft.Extensions.Logging;
using Microsoft.Xaml.Behaviors.Core;

namespace LLMClient.Endpoints;

/// <summary>
/// 作为终结点的配置中心
/// </summary>
public class EndpointConfigureViewModel : BaseViewModel, IEndpointService
{
    private readonly ILoggerFactory _loggerFactory;

    private readonly ITokensCounter _tokensCounter;


    public IAPIEndpoint? SelectedEndpoint
    {
        get => _selectedEndpoint;
        set
        {
            if (Equals(value, _selectedEndpoint)) return;
            _selectedEndpoint = value;
            OnPropertyChanged();
        }
    }

    public ICommand AddNewEndpointCommand => new ActionCommand(o =>
    {
        Endpoints.Add(new APIEndPoint(new APIEndPointOption(), _loggerFactory, _tokensCounter));
    });

    public ICommand RemoveEndPointCommand => new RelayCommand<IAPIEndpoint?>((o =>
    {
        if (o is APIEndPoint endpoint)
        {
            if (!MessageBoxes.Question($"Sure to remove the endpoint {endpoint.DisplayName} ?", "warning"))
            {
                return;
            }

            Endpoints.Remove(endpoint);
        }
    }));

    public ICommand ToggleEndpointDisabledCommand => new RelayCommand<IAPIEndpoint?>((o =>
    {
        if (o != null)
        {
            o.IsDisabled = !o.IsDisabled;
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
                difference.ForEach(endpoint => { MessageBoxes.Error("Endpoint name must be unique:" + endpoint.Name); });
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
                    MessageBoxes.Error("保存失败: " + msg, "错误");
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
            MessageBoxes.Error("保存失败: " + e.Message, "错误");
        }
    });

    public ICommand ReloadCommand => new ActionCommand((async void (o) =>
    {
        Endpoints.Clear();
        await this.Initialize();
    }));

    public ObservableCollection<IAPIEndpoint> Endpoints { get; set; } = [];

    public IReadOnlyList<IAPIEndpoint> AllEndpoints => Endpoints;

    public IReadOnlyList<IAPIEndpoint> CandidateEndpoints
    {
        get
        {
            var list = new List<IAPIEndpoint>(Endpoints.Count + 2)
            {
                _historyEndPoint,
                _suggestedEndPoint,
            };
#if DEBUG
            list.Add(_testEndPoint);
#endif
            list.AddRange(Endpoints.Where(e => !e.IsDisabled));
            return list;
        }
    }

    private readonly ModelsViewEndpoint _historyEndPoint;

    private readonly ModelsViewEndpoint _suggestedEndPoint;

    private readonly StubEndPoint _testEndPoint;

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

    public ObservableCollection<IEndpointModel> SuggestedModelsOb { get; } = [];

    public ModelSelectionPopupViewModel PopupSelectViewModel { get; }

    public ICommand RemoveSuggestedModelCommand => new ActionCommand((o =>
    {
        if (o is IEndpointModel suggestedModel)
        {
            this.SuggestedModelsOb.Remove(suggestedModel);
        }
    }));

    private IAPIEndpoint? _selectedEndpoint;

    public EndpointConfigureViewModel(ILoggerFactory loggerFactory, IMapper mapper, ITokensCounter tokensCounter)
    {
        this._loggerFactory = loggerFactory;
        this._tokensCounter = tokensCounter;
        PopupSelectViewModel = new ModelSelectionPopupViewModel(OnModelSelected)
            { SuccessRoutedCommand = PopupBox.ClosePopupCommand };
        _suggestedModels = new ReadOnlyObservableCollection<IEndpointModel>(SuggestedModelsOb);
        _historyChatModels = new ReadOnlyObservableCollection<IEndpointModel>(_historyChatModelsOb);
        _historyEndPoint = new ModelsViewEndpoint(_historyChatModelsOb)
        {
            DisplayName = "History Models",
            Name = "HistoryEndPoint",
            Icon = PackIconKind.History.GetThemedIcon(),
        };
        _suggestedEndPoint = new ModelsViewEndpoint(SuggestedModelsOb)
        {
            DisplayName = "Suggested Models",
            Name = "SuggestedEndPoint",
            Icon = PackIconKind.StarOutline.GetThemedIcon(),
        };
        _testEndPoint = new StubEndPoint("Test Endpoint");
        Endpoints.CollectionChanged += (_, e) =>
        {
            if (e.NewItems != null)
            {
                foreach (var item in e.NewItems.OfType<INotifyPropertyChanged>())
                {
                    item.PropertyChanged += OnEndpointPropertyChanged;
                }
            }

            if (e.OldItems != null)
            {
                foreach (var item in e.OldItems.OfType<INotifyPropertyChanged>())
                {
                    item.PropertyChanged -= OnEndpointPropertyChanged;
                }
            }

            OnPropertyChanged(nameof(CandidateEndpoints));
        };
    }

    private void OnEndpointPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IAPIEndpoint.IsDisabled))
        {
            OnPropertyChanged(nameof(CandidateEndpoints));
        }
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

    private readonly List<ArchivedModelTelemetry> _archivedTelemetry = [];

    public IReadOnlyList<ArchivedModelTelemetry> ArchivedTelemetry => _archivedTelemetry;

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
                        Endpoints.Add(new APIEndPoint(endPoint, _loggerFactory, _tokensCounter));
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

            PostOnPropertyChanged(nameof(SuggestedModels));
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

            PostOnPropertyChanged(nameof(HistoryModels));
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

                    // Prefer a stored DisplayName; fall back to the endpoint Name.
                    endpointNode.TryGetPropertyValue("DisplayName", out var displayNameNode);

                    var llmEndpoint = ((IEndpointService)this).GetEndpoint(endpointName);
                    var endpointDisplayName = llmEndpoint?.DisplayName
                                              ?? displayNameNode?.GetValue<string>()
                                              ?? endpointName;

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

                        if (!modelNode.TryGetPropertyValue("Telemetry", out var modelTelemetry) ||
                            modelTelemetry == null)
                        {
                            continue;
                        }

                        var usageCounter =
                            modelTelemetry.Deserialize<UsageCounter>(Extension.DefaultJsonSerializerOptions);
                        if (usageCounter == null)
                        {
                            continue;
                        }

                        if (llmEndpoint == null)
                        {
                            // Endpoint no longer exists – archive all its models.
                            _archivedTelemetry.Add(new ArchivedModelTelemetry(
                                endpointName, endpointDisplayName, modelName, usageCounter));
                            continue;
                        }

                        var model = llmEndpoint.GetModel(modelName);
                        if (model == null)
                        {
                            // Model no longer exists in this endpoint – archive it.
                            _archivedTelemetry.Add(new ArchivedModelTelemetry(
                                endpointName, endpointDisplayName, modelName, usageCounter));
                            continue;
                        }

                        model.Telemetry = usageCounter;
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
        // Build a map: endpointName -> (displayName, list of model telemetry entries)
        var endpointTelemetryMap = new Dictionary<string, (string DisplayName, List<JsonObject> Models)>();

        // Step 1: current endpoints
        foreach (var endpoint in this.Endpoints)
        {
            var modelsList = new List<JsonObject>();
            foreach (var availableModel in endpoint.AvailableModels)
            {
                if (availableModel.Telemetry != null)
                {
                    modelsList.Add(new JsonObject()
                    {
                        ["ModelName"] = availableModel.Name,
                        ["Telemetry"] = JsonSerializer.SerializeToNode(availableModel.Telemetry, options)
                    });
                }
            }

            endpointTelemetryMap[endpoint.Name] = (endpoint.DisplayName, modelsList);
        }

        // Step 2: merge archived (deleted) telemetry so it survives the next save
        foreach (var archived in _archivedTelemetry)
        {
            if (!endpointTelemetryMap.TryGetValue(archived.EndpointName, out var entry))
            {
                entry = (archived.EndpointDisplayName, new List<JsonObject>());
                endpointTelemetryMap[archived.EndpointName] = entry;
            }

            entry.Models.Add(new JsonObject()
            {
                ["ModelName"] = archived.ModelName,
                ["Telemetry"] = JsonSerializer.SerializeToNode(archived.Telemetry, options)
            });
        }

        // Step 3: build the JSON array
        var jsonArray = new JsonArray();
        foreach (var (endpointName, (displayName, models)) in endpointTelemetryMap)
        {
            if (models.Count == 0) continue;

            var modelsArray = new JsonArray();
            foreach (var m in models) modelsArray.Add(m);
            jsonArray.Add(new JsonObject
            {
                ["Name"] = endpointName,
                ["DisplayName"] = displayName,
                ["Models"] = modelsArray
            });
        }

        root[TelemetryKey] = jsonArray;
        await EndPointsConfig.SaveDoc(root);
    }
}