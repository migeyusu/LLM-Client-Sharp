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
        if (_predefinedModels.TryGetValue(modelName, out var action) &&
            _loadedModelInfos.TryGetValue(modelName, out var availableModelInfo))
        {
            action(availableModelInfo);
            return new AzureClientBase(this, availableModelInfo);
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
        Action<AzureModelInfo> full = (info) =>
        {
            info.SystemPromptEnable = true;
            info.TopPEnable = true;
            info.TopP = 1;
            info.TemperatureEnable = true;
            info.Temperature = 1;
            info.MaxTokens = 4096;
            info.FrequencyPenaltyEnable = true;
            info.FrequencyPenalty = 0;
            info.PresencePenaltyEnable = true;
            info.PresencePenalty = 0;
        };
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

        Action<AzureModelInfo> o1 = (info) => { info.SystemPromptEnable = true; };
        Action<AzureModelInfo> llama3 = (info) =>
        {
            info.SystemPromptEnable = true;
            info.TopPEnable = true;
            info.TopP = 0.1f;
            info.TemperatureEnable = true;
            info.Temperature = 0.8f;
            info.MaxTokens = 2048;
            info.PresencePenaltyEnable = true;
            info.PresencePenalty = 0;
            info.FrequencyPenaltyEnable = true;
            info.FrequencyPenalty = 0;
        };
        Action<AzureModelInfo> empty = (info) => { };
        Action<AzureModelInfo> deepSeek_R1 = (info) => { info.MaxTokens = 2048; };
        Action<AzureModelInfo> deepSeek_V3 = (info) =>
        {
            info.SystemPromptEnable = true;
            info.TopPEnable = true;
            info.TopP = 0.1f;
            info.TemperatureEnable = true;
            info.Temperature = 0.8f;
            info.MaxTokens = 2048;
            info.FrequencyPenaltyEnable = true;
            info.FrequencyPenalty = 0;
            info.PresencePenaltyEnable = true;
            info.PresencePenalty = 0;
        };
        _predefinedModels = new Dictionary<string, Action<AzureModelInfo>>()
        {
            { "OpenAI GPT-4.1", full },
            { "OpenAI GPT-4.1-mini", full },
            { "OpenAI GPT-4.1-nano", full },

            { "OpenAI GPT-4o", baseModel },
            { "OpenAI GPT-4o mini", baseModel },

            { "OpenAI o1", o1 },
            { "OpenAI o1-mini", empty },
            { "OpenAI o1-preview", empty },

            { "OpenAI o3", o1 },
            { "OpenAI o3-mini", o1 },
            { "OpenAI o4-mini", o1 },

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

            { "DeepSeek-R1", deepSeek_R1 },
            { "MAI-DS-R1", deepSeek_R1 },
            
            {"DeepSeek-V3-0324",deepSeek_V3},
            
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