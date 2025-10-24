using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Input;
using AutoMapper;
using CommunityToolkit.Mvvm.Input;
using LLMClient.Abstraction;
using LLMClient.Data;
using LLMClient.Dialog;
using LLMClient.Endpoints.Azure;
using LLMClient.Endpoints.OpenAIAPI;
using LLMClient.UI;
using LLMClient.UI.Component;
using MaterialDesignThemes.Wpf;
using Microsoft.Extensions.Logging;
using Microsoft.Xaml.Behaviors.Core;

namespace LLMClient.Endpoints;

/// <summary>
/// 作为终结点的配置中心
/// </summary>
public class EndpointConfigureViewModel : BaseViewModel, IEndpointService
{
    private ILoggerFactory _loggerFactory;
    private readonly IMapper _mapper;

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

    public ICommand AddNewEndpointCommand => new ActionCommand((o =>
    {
        Endpoints.Add(new APIEndPoint(new APIEndPointOption(), _loggerFactory));
    }));

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
            var distinctEndpoints = Endpoints.DistinctBy(endpoint => endpoint.Name);
            var difference = distinctEndpoints.Except(Endpoints).ToList();
            if (difference.Count != 0)
            {
                difference.ForEach(endpoint => { MessageBox.Show("Endpoint name must be unique:" + endpoint.Name); });
                return;
            }

            var root = new JsonObject();
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
                    MessageBox.Show(msg);
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
            // .Select(model => new KeyValuePair<string, string>(model.LlmModel.Endpoint.Name, model.LlmModel.Name)
            var serializeToNode = JsonSerializer.SerializeToNode(keyValuePairs, options);
            endPointsNode[SuggestedModelKey] = serializeToNode;
            await EndPointsConfig.SaveDoc(root);
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
        get { return Endpoints.ToArray(); }
    }

    public IReadOnlyList<ILLMChatModel> SuggestedModels
    {
        get { return SuggestedModelsOb.ToArray(); }
    }

    public ObservableCollection<ILLMChatModel> SuggestedModelsOb { get; } =
        new ObservableCollection<ILLMChatModel>();

    public ModelSelectionPopupViewModel PopupSelectViewModel { get; }

    public ICommand RemoveSuggestedModelCommand => new ActionCommand((o =>
    {
        if (o is ILLMChatModel suggestedModel)
        {
            this.SuggestedModelsOb.Remove(suggestedModel);
            OnPropertyChanged(nameof(SuggestedModels));
        }
    }));

    private ILLMEndpoint? _selectedEndpoint;

    public EndpointConfigureViewModel(ILoggerFactory loggerFactory, IMapper mapper)
    {
        this._loggerFactory = loggerFactory;
        this._mapper = mapper;
        PopupSelectViewModel = new ModelSelectionPopupViewModel(OnModelSelected)
            { SuccessRoutedCommand = PopupBox.ClosePopupCommand };
        Endpoints.CollectionChanged += EndpointsOnCollectionChanged;
    }

    private void EndpointsOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        this.OnPropertyChanged(nameof(AvailableEndpoints));
    }

    private void OnModelSelected(ILLMChatClient obj)
    {
        if (SuggestedModelsOb.Count > 6)
        {
            MessageEventBus.Publish("最多只能添加6个推荐模型");
            return;
        }

        this.SuggestedModelsOb.Add(obj.Model);
        OnPropertyChanged(nameof(SuggestedModels));
    }

    private GithubCopilotEndPoint? _githubCopilotEndPoint;

    private const string SuggestedModelKey = "SuggestedModels";

    public async Task Initialize()
    {
        var endPoints = (await EndPointsConfig.LoadEndpointsNode()).AsObject();
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
    }
}