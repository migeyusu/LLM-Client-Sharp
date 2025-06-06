using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using LLMClient.Endpoints.OpenAIAPI;
using LLMClient.UI.Component;

namespace LLMClient.Endpoints.Converters;

public class XiaoaiAIModelMapping : ModelMapping
{
    public XiaoaiAIModelMapping() : base("XiaoaiAI")
    {
    }

    public override IList<string> AvailableModels { get; } = [];

    public override Task<bool> Refresh()
    {
        throw new NotImplementedException();
    }

    public override APIModelInfo? TryGet(string modelId)
    {
        throw new NotImplementedException();
    }
}

public class GeekAIModelMapping : ModelMapping
{
    public GeekAIModelMapping() : base("GeekAI")
    {
    }

    public override IList<string> AvailableModels
    {
        get { return _modelInfos.Select((info => info.Alias)).ToArray(); }
    }

    private string[] modelType = { "chat", "search" };

    private ModelInfo[] _modelInfos = [];

    public override async Task<bool> Refresh()
    {
        var httpClient = new HttpClient();
        try
        {
            var responseMessage = await httpClient.GetAsync(new Uri("https://geekai.co/api/models?source=web"));
            await using (var stream = await responseMessage.Content.ReadAsStreamAsync())
            {
                var modelInfo = await JsonSerializer.DeserializeAsync<ModelInfo[]>(stream);
                if (modelInfo != null)
                {
                    _modelInfos = modelInfo
                        .Where((info =>
                            !string.IsNullOrEmpty(info.Name) && !string.IsNullOrEmpty(info.Alias) &&
                            modelType.Contains(info.Type)))
                        .ToArray();
                    return true;
                }
            }
        }
        catch (Exception e)
        {
            MessageEventBus.Publish($"Error fetching models from GeekAI: {e.Message}");
        }

        return false;
    }

    public override APIModelInfo? TryGet(string modelId)
    {
        var modelInfo =
            _modelInfos.FirstOrDefault(info => info.Name.Equals(modelId, StringComparison.OrdinalIgnoreCase));
        if (modelInfo != null)
        {
            return new APIModelInfo()
            {
                Name = modelInfo.Alias,
                Id = modelInfo.Name,
                Description = modelInfo.Desc,
                UrlIconEnable = !string.IsNullOrEmpty(modelInfo.Icon),
                IconUrl = modelInfo.Icon,
                MaxContextSize = modelInfo.ContextLen,
                MaxTokenLimit = modelInfo.OutputLen,
                SystemPromptEnable = modelInfo.Systemable,
            };
        }

        return null;
    }


    public class ModelInfo
    {
        [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;

        [JsonPropertyName("alias")] public string Alias { get; set; } = string.Empty;

        [JsonPropertyName("type")] public string Type { get; set; } = string.Empty;

        [JsonPropertyName("icon")] public string? Icon { get; set; }

        [JsonPropertyName("desc")] public string? Desc { get; set; }

        [JsonPropertyName("context_length")] public string? ContextLength { get; set; }

        [JsonPropertyName("context_len")] public int ContextLen { get; set; }

        [JsonPropertyName("output_length")] public string? OutputLength { get; set; }

        [JsonPropertyName("output_len")] public int OutputLen { get; set; }

        [JsonPropertyName("price")] public string? Price { get; set; }

        [JsonPropertyName("input_price")] public double InputPrice { get; set; }

        [JsonPropertyName("output_price")] public double OutputPrice { get; set; }

        [JsonPropertyName("proxy_prices")] public List<ProxyPrice>? ProxyPrices { get; set; }

        [JsonPropertyName("origin_prices")] public List<OriginPrice>? OriginPrices { get; set; }

        [JsonPropertyName("discount")] public string? Discount { get; set; }

        [JsonPropertyName("rights")] public string? Rights { get; set; }

        [JsonPropertyName("platform")] public Platform? Platform { get; set; }

        [JsonPropertyName("rate_limit")] public int RateLimit { get; set; }

        [JsonPropertyName("sort")] public int Sort { get; set; }

        [JsonPropertyName("level")] public int Level { get; set; }

        [JsonPropertyName("visible")] public bool Visible { get; set; }

        [JsonPropertyName("audioable")] public bool Audioable { get; set; }

        [JsonPropertyName("searchable")] public bool Searchable { get; set; }

        [JsonPropertyName("functionable")] public bool Functionable { get; set; }

        [JsonPropertyName("streamable")] public bool Streamable { get; set; }

        [JsonPropertyName("jsonable")] public bool Jsonable { get; set; }

        [JsonPropertyName("cacheable")] public bool Cacheable { get; set; }

        [JsonPropertyName("systemable")] public bool Systemable { get; set; }

        [JsonPropertyName("reasonable")] public bool Reasonable { get; set; }

        [JsonPropertyName("temperature")] public double Temperature { get; set; }

        [JsonPropertyName("features")] public List<string>? Features { get; set; }

        [JsonPropertyName("created_at")] public string? CreatedAt { get; set; }

        [JsonPropertyName("created_time")] public long CreatedTime { get; set; }

        [JsonPropertyName("balance")] public Balance? Balance { get; set; }
    }

    public class ProxyPrice
    {
        [JsonPropertyName("part")] public string? Part { get; set; }

        [JsonPropertyName("group")] public string? Group { get; set; }

        [JsonPropertyName("discount")] public double Discount { get; set; }

        [JsonPropertyName("labels")] public string? Labels { get; set; }
    }

    public class OriginPrice
    {
        [JsonPropertyName("part")] public string? Part { get; set; }

        [JsonPropertyName("labels")] public string? Labels { get; set; }
    }

    public class Platform
    {
        [JsonPropertyName("name")] public string? Name { get; set; }

        [JsonPropertyName("alias")] public string? Alias { get; set; }

        [JsonPropertyName("website")] public string? Website { get; set; }
    }

    public class Balance
    {
        [JsonPropertyName("remains")] public int Remains { get; set; }

        [JsonPropertyName("tokens")] public int Tokens { get; set; }
    }
}