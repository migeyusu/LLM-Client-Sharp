using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using LLMClient.Endpoints.OpenAIAPI;
using LLMClient.UI.Component;

namespace LLMClient.Endpoints.Converters;

public class O3FanModelMapping : ModelMapping
{
    public O3FanModelMapping() : base("O3.Fan")
    {
    }

    public override IList<string> AvailableModels
    {
        get { return _modelInfos.Select((info => info.Id)).ToArray(); }
    }

    private ModelInfoSimple[] _modelInfos = [];

    public override async Task<bool> Refresh()
    {
        using (var httpClient = new HttpClient())
        {
            try
            {
                using (var responseMessage =
                       await httpClient.GetAsync(new Uri("https://data.ocoolai.com/items/models?limit=1000")))
                {
                    responseMessage.EnsureSuccessStatusCode();
                    await using (var stream = await responseMessage.Content.ReadAsStreamAsync())
                    {
                        var node = await JsonNode.ParseAsync(stream);
                        if (node?.AsObject().TryGetPropertyValue("data", out var listNode) != true)
                        {
                            return false;
                        }

                        if (listNode == null)
                        {
                            return false;
                        }

                        var modelInfo = listNode.Deserialize<ModelInfoSimple[]>();
                        if (modelInfo != null)
                        {
                            _modelInfos = modelInfo
                                .Where((info =>
                                    !string.IsNullOrEmpty(info.Id) && StatusAvailable.Contains(info.Status) &&
                                    info.ModelAbility?.Contains(ModelAbilityChat) == true))
                                .ToArray();
                            return true;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                MessageEventBus.Publish($"Error fetching models from O3.Fan: {e.Message}");
            }

            return false;
        }
    }

    public override APIModelInfo? TryGet(string modelId)
    {
        var modelInfoSimple =
            _modelInfos.FirstOrDefault(info => info.Id.Equals(modelId, StringComparison.OrdinalIgnoreCase));
        if (modelInfoSimple == null)
        {
            return null;
        }

        var apiModelInfo = new APIModelInfo()
        {
            Name = modelInfoSimple.Id,
            Id = modelInfoSimple.Id,
        };
        MapInfo(apiModelInfo);
        return apiModelInfo;
    }

    public override bool MapInfo(APIModelInfo modelInfo)
    {
        var modelInfoSimple =
            _modelInfos.FirstOrDefault(info => info.Id.Equals(modelInfo.Id, StringComparison.OrdinalIgnoreCase));
        if (modelInfoSimple == null)
        {
            return false;
        }

        var modelLogo = modelInfoSimple.ModelLogo;
        if (modelLogo != null)
        {
            modelInfo.UrlIconEnable = true;
            modelInfo.IconUrl = $"https://o3.fan/info/models/assess/{modelLogo}.png";
        }

        modelInfo.Description = modelInfoSimple.Description;
        modelInfo.MaxContextSize = (modelInfoSimple.ContextLength ?? 0) * 1000;
        modelInfo.MaxTokensEnable = modelInfoSimple.MaxOutput != null;
        modelInfo.MaxTokenLimit = (modelInfoSimple.MaxOutput ?? 0) * 1000;
        var modelAbility = modelInfoSimple.ModelAbility;
        if (modelAbility != null)
        {
            modelInfo.SupportTextGeneration = modelAbility.Contains("对话");
            modelInfo.SupportImageInput = modelAbility.Contains("视觉");
            modelInfo.Reasonable = modelAbility.Contains("推理");
            modelInfo.SupportFunctionCall = modelAbility.Contains("工具调用");
            modelInfo.SupportAudioGeneration = modelAbility.Contains("文本转语音");
            if (modelAbility.Contains("语音转文本"))
            {
                modelInfo.SupportAudioInput = true;
                modelInfo.SupportTextGeneration = true;
            }

            if (modelAbility.Contains("联网搜索"))
            {
                modelInfo.SupportSearch = true;
            }
        }

        return true;
    }

    string[] StatusAvailable = ["published", "draft"];

    const string ModelAbilityChat = "对话";

    public class ModelInfoSimple
    {
        [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;

        [JsonPropertyName("status")] public string? Status { get; set; }

        [JsonPropertyName("date_updated")] public string? DateUpdated { get; set; }

        [JsonPropertyName("context_length")] public int? ContextLength { get; set; }

        [JsonPropertyName("max_output")] public int? MaxOutput { get; set; }

        [JsonPropertyName("release_date")] public string? ReleaseDate { get; set; }

        [JsonPropertyName("description")] public string? Description { get; set; }

        [JsonPropertyName("badge")] public string? Badge { get; set; }

        [JsonPropertyName("priority")] public int? Priority { get; set; }

        [JsonPropertyName("model_logo")] public string? ModelLogo { get; set; }

        [JsonPropertyName("provider")] public string? Provider { get; set; }

        [JsonPropertyName("tag")] public List<string>? Tag { get; set; }

        [JsonPropertyName("model_ablity")] public List<string>? ModelAbility { get; set; }

        [JsonPropertyName("data_type")] public string? DataType { get; set; }
    }
}