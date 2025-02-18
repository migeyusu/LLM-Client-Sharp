using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using AutoMapper;
using LLMClient.Azure.Models;

namespace LLMClient.Azure;

public class GithubCopilotEndPoint : AzureOption, ILLMEndpoint
{
    private readonly Dictionary<string, ModelCreation> _availableModels;

    private readonly Dictionary<string, AzureModelInfo> _availableModelInfos = new Dictionary<string, AzureModelInfo>();

    public string DisplayName { get; } = "Github Copilot";
    public string Name { get; } = "Azure";

    public IList<string> AvailableModels
    {
        get { return _availableModels.Keys.ToArray(); }
    }

    public ICollection<AzureModelInfo> AvailableModelsInfos
    {
        get { return _availableModelInfos.Values; }
    }

    private readonly IMapper _mapper;

    public ILLMModel? GetModel(string modelName)
    {
        if (_availableModels.TryGetValue(modelName, out var availableModel) &&
            _availableModelInfos.TryGetValue(modelName, out var availableModelInfo))
        {
            return availableModel.CreateModel(this, availableModelInfo);
        }

        return null;
    }

    public void UpdateConfig(JsonNode document)
    {
        var config = JsonSerializer.SerializeToNode<AzureOption>(this);
        document["AzureOptions"] = config;
    }

    public void ReloadConfig(JsonNode document)
    {
        var jsonNode = document["AzureOptions"];
        if (jsonNode == null)
        {
            return;
        }

        var azureOption = jsonNode.Deserialize<AzureOption>();
        if (azureOption == null)
        {
            return;
        }

        _mapper.Map<AzureOption, GithubCopilotEndPoint>(azureOption, this);
    }

    public GithubCopilotEndPoint(IMapper mapper)
    {
        this._mapper = mapper;
        var mistral = new ModelCreation<AzureTextModelBase>(model =>
        {
            model.MaxTokens = 2048;
            model.Temperature = 0.8f;
            model.TopP = 0.1f;
        });
        var baseModel = new ModelCreation<AzureTextModelBase>();
        var llama3 = new ModelCreation<MetaLlama3>();
        var o1 = new ModelCreation<OpenAIO1>();
        var empty = new ModelCreation<AzureModelBase>();
        _availableModels = new Dictionary<string, ModelCreation>()
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

            { "DeepSeek-R1", new ModelCreation<DeepSeekR1>() }
        };
    }

    public async Task InitializeAsync()
    {
        //load models
        var path = Path.GetFullPath(Path.Combine("Azure", "Models", "models.json"));
        var fileInfo = new FileInfo(path);
        if (!fileInfo.Exists)
        {
            return;
        }

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

                if (_availableModels.ContainsKey(modelInfo.Name))
                {
                    await modelInfo.InitializeAsync();
                    modelInfo.Endpoint = this;
                    _availableModelInfos.Add(modelInfo.Name, modelInfo);
                }
            }
        }
    }
}