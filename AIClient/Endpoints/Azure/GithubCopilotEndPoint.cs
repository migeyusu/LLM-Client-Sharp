using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LLMClient.Abstraction;
using LLMClient.Endpoints.Azure.Models;

namespace LLMClient.Endpoints.Azure;

public sealed class GithubCopilotEndPoint : AzureEndPointBase
{
    public const string ModelInfoUrl = "https://github.com/models/available";

    public const string GithubCopilotName = "Github Copilot";

    private readonly Dictionary<string, Action<AzureModelInfo>> _predefinedModels;

    /// <summary>
    /// key: model-id
    /// </summary>
    private readonly Dictionary<string, AzureModelInfo> _loadedModelInfos = new Dictionary<string, AzureModelInfo>();

    public override bool IsDefault { get; } = true;
    public override string Name { get; } = GithubCopilotName;

    private static readonly Lazy<ImageSource> Source = new Lazy<ImageSource>((() =>
    {
        var bitmapImage = new BitmapImage(new Uri(
            @"pack://application:,,,/LLMClient;component/Resources/Images/github-copilot-icon.png",
            UriKind.Absolute));
        bitmapImage.Freeze();
        return bitmapImage;
    }));

    public override ImageSource Icon
    {
        get { return Source.Value; }
    }

    public override IReadOnlyCollection<ILLMModel> AvailableModels
    {
        get
        {
            return this._loadedModelInfos.Values.Where(info => info.IsEnabled).ToArray();
            ;
        }
    }

    public IReadOnlyCollection<AzureModelInfo> TotalModelsInfos
    {
        get { return _loadedModelInfos.Values; }
    }

    public override ILLMChatClient? NewClient(string modelName)
    {
        if (_predefinedModels.TryGetValue(modelName, out var action) &&
            _loadedModelInfos.TryGetValue(modelName, out var availableModelInfo))
        {
            action(availableModelInfo);
            return new AzureClientBase(this, availableModelInfo);
        }

        return null;
    }

    public override ILLMChatClient? NewClient(ILLMModel model)
    {
        if (model is AzureModelInfo azureModelInfo)
        {
            return new AzureClientBase(this, azureModelInfo);
        }

        return null;
    }

    public override ILLMModel? GetModel(string modelName)
    {
        return _loadedModelInfos.GetValueOrDefault(modelName);
    }

    public void UpdateConfig(JsonNode document)
    {
        var config = JsonSerializer.SerializeToNode(this.Option);
        document[Name] = config;
    }

    public static GithubCopilotEndPoint TryLoad(JsonObject document)
    {
        var githubCopilotEndPoint = new GithubCopilotEndPoint();
        if (document.TryGetPropertyValue(GithubCopilotName, out var jsonNode))
        {
            var azureOption = jsonNode?.Deserialize<AzureOption>();
            if (azureOption != null)
            {
                githubCopilotEndPoint.Option = azureOption;
            }
        }

        return githubCopilotEndPoint;
    }

    public GithubCopilotEndPoint()
    {
        Action<AzureModelInfo> full = (info) =>
        {
            info.TopP = 1;
            info.Temperature = 1;
            info.FrequencyPenalty = 0;
            info.PresencePenalty = 0;
        };
        Action<AzureModelInfo> mistral = (info) =>
        {
            info.MaxTokens = 2048;
            info.Temperature = 0.8f;
            info.TopP = 0.1f;
        };
        Action<AzureModelInfo> baseModel = (info) =>
        {
            info.TopP = 1;
            info.Temperature = 1;
        };

        Action<AzureModelInfo> llama3 = (info) =>
        {
            info.TopP = 0.1f;
            info.Temperature = 0.8f;
            info.MaxTokens = 2048;
            info.PresencePenalty = 0;
            info.FrequencyPenalty = 0;
        };
        Action<AzureModelInfo> empty = (info) => { };
        Action<AzureModelInfo> deepSeek_R1 = (info) => { info.MaxTokens = 2048; };
        Action<AzureModelInfo> deepSeek_V3 = (info) =>
        {
            info.TopP = 0.1f;
            info.Temperature = 0.8f;
            info.MaxTokens = 2048;
            info.FrequencyPenalty = 0;
            info.PresencePenalty = 0;
        };
        Action<AzureModelInfo> phi4 = (info) =>
        {
            info.MaxTokens = 2048;
            info.Temperature = 0.8f;
            info.TopP = 0.1f;
            info.PresencePenalty = 0;
            info.PresencePenalty = 0;
        };

        _predefinedModels = new Dictionary<string, Action<AzureModelInfo>>()
        {
            { "OpenAI GPT-4.1", full },
            { "OpenAI GPT-4.1-mini", full },
            { "OpenAI GPT-4.1-nano", full },

            { "OpenAI GPT-4o", baseModel },
            { "OpenAI GPT-4o mini", baseModel },

            { "OpenAI o1", empty },

            { "OpenAI o3", empty },
            { "OpenAI o3-mini", empty },
            { "OpenAI o4-mini", empty },

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

            { "Llama 4 Maverick 17B 128E Instruct FP8", llama3 },
            { "Llama 4 Scout 17B 16E Instruct", llama3 },

            { "DeepSeek-R1", deepSeek_R1 },
            { "MAI-DS-R1", deepSeek_R1 },
            { "DeepSeek-R1-0528", deepSeek_R1 },

            { "DeepSeek-V3-0324", deepSeek_V3 },

            { "Phi-4", phi4 },
            { "Phi-4-mini-instruct", phi4 },
            { "Phi-4-mini-reasoning", phi4 },
            { "Phi-4-multimodal-instruct", phi4 },
            { "Phi-4-reasoning", phi4 }
        };
    }

    private async Task FetchModelsFromHttp()
    {
        using (var httpClient = new HttpClient())
        {
            var responseMessage = await httpClient.GetAsync(ModelInfoUrl);
            await using (var stream = await responseMessage.Content.ReadAsStreamAsync())
            {
                await DeserializeModels(stream);
            }
        }
    }

    private async Task FetchModelsFromLocal()
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
                await DeserializeModels(fileStream);
            }
        }
        catch (Exception e)
        {
            Trace.TraceError(e.ToString());
        }
    }

    private async Task DeserializeModels(Stream stream)
    {
        _loadedModelInfos.Clear();
        var azureModelInfos = await JsonSerializer.DeserializeAsync<AzureModelInfo[]>(stream);
        if (azureModelInfos == null)
        {
            return;
        }

        foreach (var modelInfo in
                 azureModelInfos.Where((info => info.ModelTask == AzureModelInfo.FilteredTask
                                                && info.SupportedInputModalities?.Contains(AzureModelInfo
                                                    .FilteredInputText) == true)))
        {
            var modelInfoName = modelInfo.FriendlyName;
            if (_predefinedModels.ContainsKey(modelInfoName))
            {
                modelInfo.Endpoint = this;
                modelInfo.IsEnabled = true;
            }

            _loadedModelInfos.Add(modelInfoName, modelInfo);
        }
    }

    public override async Task InitializeAsync()
    {
        await FetchModelsFromLocal();
    }
}