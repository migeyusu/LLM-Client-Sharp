using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LLMClient.Abstraction;
using LLMClient.Component.CustomControl;
using LLMClient.Data;
using LLMClient.Endpoints.Azure.Models;

namespace LLMClient.Endpoints.Azure;

public sealed class GithubCopilotEndPoint : AzureEndPointBase
{
    public const string ModelInfoUrl = "https://github.com/models/available";

    public const string GithubCopilotName = "Github Copilot";

    private readonly Dictionary<string, Action<IModelParams>> _predefinedModels;

    /// <summary>
    /// key: model-id
    /// </summary>
    private readonly Dictionary<string, AzureModelInfo> _loadedModelInfos = new();

    public override bool IsInbuilt => true;
    public override string Name { get; } = GithubCopilotName;

    private static readonly Lazy<ThemedIcon> Source = new(() => { return ModelIconType.GithubCopilot.GetIcon(); });

    public override ThemedIcon Icon
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

    public GithubCopilotEndPoint()
    {
        Action<IModelParams> full = (info) =>
        {
            info.TopP = 1;
            info.Temperature = 1;
            info.FrequencyPenalty = 0;
            info.PresencePenalty = 0;
        };
        Action<IModelParams> mistral = (info) =>
        {
            info.MaxTokens = 2048;
            info.Temperature = 0.8f;
            info.TopP = 0.1f;
        };
        Action<IModelParams> baseModel = (info) =>
        {
            info.TopP = 1;
            info.Temperature = 1;
        };

        Action<IModelParams> llama3 = (info) =>
        {
            info.TopP = 0.1f;
            info.Temperature = 0.8f;
            info.MaxTokens = 2048;
            info.PresencePenalty = 0;
            info.FrequencyPenalty = 0;
        };
        Action<IModelParams> empty = (info) => { };
        Action<IModelParams> deepSeek_R1 = (info) => { info.MaxTokens = 2048; };
        Action<IModelParams> deepSeek_V3 = (info) =>
        {
            info.TopP = 0.1f;
            info.Temperature = 0.8f;
            info.MaxTokens = 2048;
            info.FrequencyPenalty = 0;
            info.PresencePenalty = 0;
        };
        Action<IModelParams> phi4 = (info) =>
        {
            info.MaxTokens = 2048;
            info.Temperature = 0.8f;
            info.TopP = 0.1f;
            info.PresencePenalty = 0;
            info.PresencePenalty = 0;
        };
        Action<IModelParams> gpt_5 = (info) => { info.MaxTokens = 16384; };

        _predefinedModels = new Dictionary<string, Action<IModelParams>>()
        {
            { "OpenAI gpt-5", gpt_5 },
            {
                "OpenAI gpt-5-chat (preview)", (info =>
                {
                    info.TopP = 1;
                    info.Temperature = 1;
                    info.MaxTokens = 16384;
                })
            },
            { "OpenAI gpt-5-mini", gpt_5 },
            { "OpenAI gpt-5-nano", gpt_5 },

            { "OpenAI GPT-4.1", full },
            { "OpenAI GPT-4.1-mini", full },
            { "OpenAI GPT-4.1-nano", full },

            { "OpenAI GPT-4o", baseModel },
            { "OpenAI GPT-4o mini", baseModel },

            { "OpenAI o1", empty },
            { "OpenAI o1-mini", empty },

            { "OpenAI o3", empty },
            { "OpenAI o3-mini", empty },

            { "OpenAI o4-mini", empty },

            { "Ministral 3B", mistral },
            { "Mistral Large 24.11", mistral },
            { "Mistral Nemo", mistral },

            { "Mistral Medium 3 (25.05)", mistral },
            { "Mistral Small 3.1", mistral },

            { "Codestral 25.01", mistral },

            { "Llama-3.3-70B-Instruct", llama3 },
            { "Meta-Llama-3.1-405B-Instruct", llama3 },
            { "Meta-Llama-3.1-8B-Instruct", llama3 },

            { "Llama 4 Maverick 17B 128E Instruct FP8", llama3 },
            { "Llama 4 Scout 17B 16E Instruct", llama3 },

            { "DeepSeek-R1-0528", deepSeek_R1 },
            { "DeepSeek-V3-0324", deepSeek_V3 },

            { "Phi-4", phi4 },
            { "Phi-4-mini-instruct", phi4 },
            { "Phi-4-mini-reasoning", phi4 },
            { "Phi-4-multimodal-instruct", phi4 },
            { "Phi-4-reasoning", phi4 }
        };
    }

    public override ILLMChatClient? NewChatClient(ILLMModel model)
    {
        if (model is AzureModelInfo azureModelInfo)
        {
            var azureClientBase = new AzureClientBase(this, azureModelInfo);
            if (_predefinedModels.TryGetValue(model.Name, out var action))
            {
                action(azureClientBase.Parameters);
                return azureClientBase;
            }
        }

        return null;
    }

    public override ILLMModel? GetModel(string modelName)
    {
        return _loadedModelInfos.GetValueOrDefault(modelName);
    }

    public void UpdateConfig(JsonNode document)
    {
        var config = JsonSerializer.SerializeToNode(this.Option, Extension.DefaultJsonSerializerOptions);
        document[Name] = config;
    }

    public static GithubCopilotEndPoint TryLoad(JsonObject document)
    {
        var githubCopilotEndPoint = new GithubCopilotEndPoint();
        if (document.TryGetPropertyValue(GithubCopilotName, out var jsonNode))
        {
            var azureOption = jsonNode?.Deserialize<AzureOption>(Extension.DefaultJsonSerializerOptions);
            if (azureOption != null)
            {
                githubCopilotEndPoint.Option = azureOption;
            }
        }

        return githubCopilotEndPoint;
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