using System.Drawing;
using System.IO;
using System.Text.Json;
using Azure.AI.Inference;
using LLMClient.Azure.Models;

namespace LLMClient.Azure;

public class AzureOption : BaseViewModel, ILLMEndpoint
{
    private string _apiToken = "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";

    public string APIToken
    {
        get => _apiToken;
        set
        {
            if (value == _apiToken) return;
            _apiToken = value;
            OnPropertyChanged();
        }
    }

    private readonly Dictionary<string, Type> _availableModels;

    private readonly Dictionary<string, AzureModelInfo> _availableModelInfos = new Dictionary<string, AzureModelInfo>();


    public string Name { get; } = "Azure";

    public IList<string> AvailableModels
    {
        get { return _availableModels.Keys.ToArray(); }
    }

    public ICollection<AzureModelInfo> AvailableModelsInfos
    {
        get { return _availableModelInfos.Values; }
    }

    private readonly AzureClient _client;

    public ILLMModel? GetModel(string modelName)
    {
        if (_availableModels.TryGetValue(modelName, out var availableModel) &&
            _availableModelInfos.TryGetValue(modelName, out var availableModelInfo))
        {
            return (ILLMModel?)Activator.CreateInstance(availableModel, _client, availableModelInfo);
        }

        return null;
    }

    public AzureOption()
    {
        _client = new AzureClient(apiToken: _apiToken);
        _availableModels = new Dictionary<string, Type>()
        {
            { "OpenAI GPT-4o", typeof(Gpt4o) },
            { "OpenAI GPT-4o mini", typeof(Gpt4OMini) },
            { "Meta-Llama-3.1-405B-Instruct", typeof(MetaLlama3_1405B_Instruct) },
            { "OpenAI o1-preview", typeof(OpenAIO1Preview) },
            { "OpenAI o1", typeof(OpenAIO1) },
            { "OpenAI o1-mini", typeof(OpenAIO1Mini) }
        };
    }

    public async Task Initialize()
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

                if (_availableModels.TryGetValue(modelInfo.Name, out var type))
                {
                    _availableModelInfos.Add(modelInfo.Name, modelInfo);
                }
            }
        }
    }
}