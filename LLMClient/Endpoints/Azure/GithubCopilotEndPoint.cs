using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
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

    public override IReadOnlyCollection<IEndpointModel> AvailableModels
    {
        get { return this._loadedModelInfos.Values.Where(info => info.IsEnabled).ToArray(); }
    }

    public IReadOnlyCollection<AzureModelInfo> TotalModelsInfos
    {
        get { return _loadedModelInfos.Values; }
    }

    public GithubCopilotEndPoint()
    {
        Action<IModelParams> gpt4 = (info) =>
        {
            info.TopP = 1;
            info.Temperature = 1;
            info.FrequencyPenalty = 0;
            info.PresencePenalty = 0;
            info.MaxTokens = 8192;
        };
        Action<IModelParams> mistral = (info) =>
        {
            info.MaxTokens = 4096;
            info.Temperature = 0.8f;
            info.TopP = 0.1f;
        };
        Action<IModelParams> gpt4o = (info) =>
        {
            info.TopP = 1;
            info.Temperature = 1;
            info.MaxTokens = 8192;
        };

        Action<IModelParams> llama3 = (info) =>
        {
            info.TopP = 0.1f;
            info.Temperature = 0.8f;
            info.MaxTokens = 2048;
            info.PresencePenalty = 0;
            info.FrequencyPenalty = 0;
        };
        Action<IModelParams> o1 = (info) => { info.MaxTokens = 8192; };
        Action<IModelParams> o3 = (info) => { info.MaxTokens = 12288; };
        Action<IModelParams> deepSeek_R1 = (info) => { info.MaxTokens = 4096; };
        Action<IModelParams> deepSeek_V3 = (info) =>
        {
            info.TopP = 0.1f;
            info.Temperature = 0.8f;
            info.MaxTokens = 4096;
            info.FrequencyPenalty = 0;
            info.PresencePenalty = 0;
        };
        Action<IModelParams> phi4 = (info) =>
        {
            info.MaxTokens = 4096;
            info.Temperature = 0.8f;
            info.TopP = 0.1f;
            info.PresencePenalty = 0;
            info.PresencePenalty = 0;
        };
        Action<IModelParams> gpt5 = (info) => { info.MaxTokens = 16384; };

        _predefinedModels = new Dictionary<string, Action<IModelParams>>()
        {
            { "OpenAI gpt-5", gpt5 },
            {
                "OpenAI gpt-5-chat (preview)", (info =>
                {
                    info.TopP = 1;
                    info.Temperature = 1;
                    info.MaxTokens = 16384;
                })
            },
            { "OpenAI gpt-5-mini", gpt5 },
            { "OpenAI gpt-5-nano", gpt5 },

            { "OpenAI GPT-4.1", gpt4 },
            { "OpenAI GPT-4.1-mini", gpt4 },
            { "OpenAI GPT-4.1-nano", gpt4 },

            { "OpenAI GPT-4o", gpt4o },
            {
                "OpenAI GPT-4o mini", (info) =>
                {
                    info.TopP = 1;
                    info.Temperature = 1;
                }
            },

            { "OpenAI o1", o1 },
            { "OpenAI o1-mini", o1 },

            { "OpenAI o3", o3 },
            { "OpenAI o3-mini", o3 },

            { "OpenAI o4-mini", o3 },

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

    public override ILLMChatClient? NewChatClient(IEndpointModel model)
    {
        if (model is AzureModelInfo azureModelInfo)
        {
            return new AzureClientBase(this, azureModelInfo);
        }

        return null;
    }

    public override IEndpointModel? GetModel(string modelName)
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
        var path = Path.GetFullPath(Path.Combine("Resources", "Test", "github_copilot.json"));
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

        foreach (var azureModelInfo in
                 azureModelInfos.Where((info => info.ModelTask == AzureModelInfo.FilteredTask
                                                && info.SupportedInputModalities?.Contains(AzureModelInfo
                                                    .FilteredInputText) == true)))
        {
            var modelInfoName = azureModelInfo.FriendlyName;
            if (_predefinedModels.TryGetValue(modelInfoName, out var setupAction))
            {
                setupAction(azureModelInfo);
                azureModelInfo.Endpoint = this;
                azureModelInfo.IsEnabled = true;
            }

            _loadedModelInfos.Add(modelInfoName, azureModelInfo);
        }
    }

    public override async Task InitializeAsync()
    {
        await FetchModelsFromLocal();
    }
}