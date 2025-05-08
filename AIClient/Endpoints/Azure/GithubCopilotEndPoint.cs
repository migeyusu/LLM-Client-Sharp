using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LLMClient.Endpoints.Azure.Models;
using Microsoft.Xaml.Behaviors.Core;

namespace LLMClient.Endpoints.Azure;

public class GithubCopilotEndPoint : AzureEndPointBase
{
    private readonly Dictionary<string, Action<AzureModelInfo>> _predefinedModels;

    private readonly Dictionary<string, AzureModelInfo> _loadedModelInfos = new Dictionary<string, AzureModelInfo>();

    public override string Name { get; } = InternalEndpoints.GithubCopilotName;

    private static readonly Lazy<ImageSource> Source = new Lazy<ImageSource>((() =>
    {
        var bitmapImage = new BitmapImage(new Uri(@"pack://application:,,,/"
                                                  + Assembly.GetExecutingAssembly().GetName().Name
                                                  + ";component/"
                                                  + "Resources/Images/github-copilot-icon.png", UriKind.Absolute));
        bitmapImage.Freeze();
        return bitmapImage;
    }));

    public override ImageSource? Icon
    {
        get { return Source.Value; }
    }

    public override IList<string> AvailableModelNames
    {
        get { return _loadedModelInfos.Keys.ToArray(); }
    }

    public ICollection<AzureModelInfo> AvailableModelsInfos
    {
        get { return _loadedModelInfos.Values; }
    }

    public ICommand ReloadCommand => new ActionCommand((async o =>
    {
        var loadEndpointsNode = await EndPointsConfiguration.LoadEndpointsNode();
        var selectedEndpoint = loadEndpointsNode.GetOrCreate(this.Name);
        this.LoadConfig(selectedEndpoint);
    }));

    public override ILLMModelClient? NewClient(string modelName)
    {
        if (_predefinedModels.TryGetValue(modelName, out var availableModel) &&
            _loadedModelInfos.TryGetValue(modelName, out var availableModelInfo))
        {
            AzureEndPointBase endPoint, AzureModelInfo modelInfo
            return availableModel.CreateModel(this, availableModelInfo);
        }

        return null;
    }

    public void UpdateConfig(JsonNode document)
    {
        var config = JsonSerializer.SerializeToNode(this.Option);
        document[Name] = config;
    }

    public void LoadConfig(JsonNode document)
    {
        var jsonNode = document[Name];
        if (jsonNode == null)
        {
            return;
        }

        var azureOption = jsonNode.Deserialize<AzureOption>();
        if (azureOption == null)
        {
            return;
        }

        this.Option = azureOption;
    }

    public GithubCopilotEndPoint()
    {
        Action<AzureModelInfo> mistral = (info) =>
        {
            info.SystemPromptEnable = true;
            info.TopPEnable = true;
            info.TemperatureEnable = true;
            info.MaxTokens = 2048;
            info.Temperature = 0.8f;
            info.TopP = 0.1f;
        };
        Action<AzureModelInfo> baseModel = (info) =>
        {
            info.SystemPromptEnable = true;
            info.TopPEnable = true;
            info.TopP = 1;
            info.TemperatureEnable = true;
            info.Temperature = 1;
            info.MaxTokens = 4096;
        };
        var llama3 = new AzureModelCreation<MetaLlama3>();
        var o1 = new AzureModelCreation<OpenAIO1>();
        var empty = new AzureModelCreation<AzureClientBase>();
        _predefinedModels = new Dictionary<string, Action<AzureModelInfo>>()
        {
            { "OpenAI GPT-4o", baseModel },
            { "OpenAI GPT-4o mini", baseModel },

            { "Ministral 3B", mistral },
            { "Mistral Large 24.11", mistral },
            { "Mistral Nemo", mistral },
            { "Mistral Large", mistral },
            { "Mistral Large (2407)", mistral },
            { "Mistral Small", mistral },
            { "Codestral 25.01", mistral },

            { "Llama-3.3-70B-Instruct", llama3 },
            { "Meta-Llama-3.1-405B-Instruct", llama3 },
            { "Meta-Llama-3.1-70B-Instruct", llama3 },
            { "Meta-Llama-3.1-8B-Instruct", llama3 },
            { "Meta-Llama-3-8B-Instruct", llama3 },
            { "Meta-Llama-3-70B-Instruct", llama3 },

            { "OpenAI o1", o1 },
            { "OpenAI o1-mini", empty },
            { "OpenAI o3-mini", o1 },
            { "OpenAI o1-preview", empty },

            { "DeepSeek-R1", new AzureModelCreation<DeepSeekR1>() }
        };
    }

    public override async Task InitializeAsync()
    {
        //load models
        var path = Path.GetFullPath(Path.Combine("EndPoints", "Azure", "Models", "models.json"));
        var fileInfo = new FileInfo(path);
        if (!fileInfo.Exists)
        {
            return;
        }

        try
        {
            using (var fileStream = fileInfo.OpenRead())
            {
                var modelsDocument = await JsonDocument.ParseAsync(fileStream);
                foreach (var element in modelsDocument.RootElement.EnumerateArray())
                {
                    var modelInfo = element.Deserialize<AzureModelInfo>();
                    if (modelInfo == null)
                    {
                        continue;
                    }

                    var modelInfoName = modelInfo.FriendlyName;
                    if (_predefinedModels.ContainsKey(modelInfoName))
                    {
                        await modelInfo.InitializeAsync();
                        modelInfo.Endpoint = this;
                        _loadedModelInfos.Add(modelInfoName, modelInfo);
                    }
                }
            }
        }
        catch (Exception e)
        {
            Trace.TraceError(e.ToString());
        }
    }
}