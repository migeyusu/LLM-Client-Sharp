using System.Net.Http;
using System.Text.Json;
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
        var httpClient = new HttpClient();
        try
        {
            var responseMessage =
                await httpClient.GetAsync(new Uri("https://data.ocoolai.com/items/models?limit=1000"));
            await using (var stream = await responseMessage.Content.ReadAsStreamAsync())
            {
                var modelInfo = await JsonSerializer.DeserializeAsync<ModelInfoSimple[]>(stream);
                if (modelInfo != null)
                {
                    _modelInfos = modelInfo
                        .Where((info =>
                            !string.IsNullOrEmpty(info.Id) && info.Status == statusAvailable &&
                            info.ModelAblity?.Contains(modelAblityChat) == true))
                        .ToArray();
                    return true;
                }
            }
        }
        catch (Exception e)
        {
            MessageEventBus.Publish($"Error fetching models from O3.Fan: {e.Message}");
        }

        return false;
    }

    public override APIModelInfo? TryGet(string modelId)
    {
        var modelInfo =
            _modelInfos.FirstOrDefault(info => info.Id.Equals(modelId, StringComparison.OrdinalIgnoreCase));
        if (modelInfo != null)
        {
            ModelIconType iconType = ModelIconType.None;
            var modelLogo = modelInfo.ModelLogo;
            if (!string.IsNullOrEmpty(modelLogo))
            {
                if (modelLogo.StartsWith("gpt"))
                {
                    iconType = ModelIconType.ChatGpt;
                }
                else
                {
                    Enum.TryParse<ModelIconType>(modelLogo, true, out iconType);
                }
            }

            return new APIModelInfo()
            {
                Name = modelInfo.Id,
                Id = modelInfo.Id,
                IconType = iconType,
                Description = modelInfo.Description,
                UrlIconEnable = false,
                MaxContextSize = (modelInfo.ContextLength ?? 0) * 1000,
                MaxTokenLimit = (modelInfo.MaxOutput ?? 0) * 1000,
            };
        }

        return null;
    }

    public override void MapInfo(APIModelInfo modelInfo)
    {
        
    }

    const string statusAvailable = "published";

    const string modelAblityChat = "对话";

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

        [JsonPropertyName("priority")] public string? Priority { get; set; }

        [JsonPropertyName("model_logo")] public string? ModelLogo { get; set; }

        [JsonPropertyName("provider")] public string? Provider { get; set; }

        [JsonPropertyName("tag")] public string? Tag { get; set; }

        [JsonPropertyName("model_ablity")] public List<string>? ModelAblity { get; set; }

        [JsonPropertyName("data_type")] public string? DataType { get; set; }
    }
}