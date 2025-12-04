using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using LLMClient.Endpoints.OpenAIAPI;
using LLMClient.UI.Component.Utility;

namespace LLMClient.Endpoints.Converters;

public class XiaoAIModelMapping : ModelMapping
{
    public virtual string Url { get; set; } = "https://xiaoai.plus/api/pricing";

    public XiaoAIModelMapping() : this("XiaoAI")
    {
    }

    public XiaoAIModelMapping(string name) : base(name)
    {
    }

    public override IList<string> AvailableModels
    {
        get { return _modelInfos.Keys.ToArray(); }
    }

    private Dictionary<string, ModelDetails> _modelInfos = new();

    private const string EnabledGroup = "default";

    public override async Task<bool> Refresh()
    {
        try
        {
            using (var handler = new HttpClientHandler())
            {
                handler.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
                using (var httpClient = new HttpClient(handler))
                {
                    using (var httpRequestMessage =
                           new HttpRequestMessage(HttpMethod.Get, Url))
                    {
                        httpRequestMessage.Headers.Add("Referrer-Policy", "strict-origin-when-cross-origin");
                        using (var message = await httpClient.SendAsync(httpRequestMessage))
                        {
                            message.EnsureSuccessStatusCode();
                            var content = await message.Content.ReadAsStringAsync();
                            var node = JsonNode.Parse(content);
                            if (node?.AsObject().TryGetPropertyValue("data", out var listNode) != true)
                            {
                                return false;
                            }

                            var modelDetails = listNode?.Deserialize<ModelDetails[]>();
                            if (modelDetails == null)
                            {
                                return false;
                            }

                            this._modelInfos = modelDetails
                                // .Where(modelDetail => modelDetail.EnableGroups?.Contains(EnabledGroup) == true)
                                .ToDictionary(modelDetail => modelDetail.ModelName);
                            return true;
                        }
                    }
                }
            }
        }
        catch (Exception e)
        {
            MessageEventBus.Publish($"Error fetching models from XiaoAI: {e.Message}");
        }

        return false;
    }

    public override APIModelInfo? TryGet(string modelId)
    {
        var apiModelInfo = new APIModelInfo()
        {
            Id = modelId,
        };
        this.MapInfo(apiModelInfo);
        return apiModelInfo;
    }

    public override bool MapInfo(APIModelInfo modelInfo)
    {
        var modelInfoId = modelInfo.Id;
        if (_modelInfos.TryGetValue(modelInfoId, out var modelDetails))
        {
            if (modelInfo.PriceCalculator is TokenBasedPriceCalculator calculator)
            {
                calculator.DiscountFactor = 1;
                calculator.InputPrice = modelDetails.ModelRatio * 2;
                calculator.OutputPrice = modelDetails.ModelRatio * 2 * modelDetails.CompletionRatio;
            }

            if (!string.IsNullOrEmpty(modelDetails.Description))
            {
                modelInfo.Description = modelDetails.Description;
            }

            var tags = modelDetails.Tags;
            if (tags != null)
            {
                if (tags.Contains("绘画"))
                {
                    modelInfo.SupportImageGeneration = true;
                }

                if (tags.Contains("音频"))
                {
                    modelInfo.SupportAudioGeneration = true;
                }

                if (tags.Contains("对话"))
                {
                    modelInfo.SupportTextGeneration = true;
                }

                if (tags.Contains("识图"))
                {
                    modelInfo.SupportImageInput = true;
                }

                if (tags.Contains("思考"))
                {
                    modelInfo.Reasonable = true;
                }

                if (tags.Contains("视频生成"))
                {
                    modelInfo.SupportVideoGeneration = true;
                }

                if (tags.Contains("联网"))
                {
                    modelInfo.SupportSearch = true;
                }

                if (tags.Contains("工具"))
                {
                    modelInfo.SupportFunctionCall = true;
                }

                if (tags.Contains("文本转语音"))
                {
                    modelInfo.SupportAudioGeneration = true;
                }
            }

            return true;
        }

        return false;
    }

    public class ModelDetails
    {
        /// <summary>
        /// 模型名称
        /// </summary>
        [JsonPropertyName("model_name")]
        public string ModelName { get; set; } = string.Empty;

        /// <summary>
        /// 配额类型
        /// </summary>
        [JsonPropertyName("quota_type")]
        public int QuotaType { get; set; }

        /// <summary>
        /// 模型权重比
        /// </summary>
        [JsonPropertyName("model_ratio")]
        public float ModelRatio { get; set; }

        /// <summary>
        /// 模型价格
        /// </summary>
        [JsonPropertyName("model_price")]
        public float ModelPrice { get; set; }

        /// <summary>
        /// 拥有者信息
        /// </summary>
        [JsonPropertyName("owner_by")]
        public string OwnerBy { get; set; } = string.Empty;

        /// <summary>
        /// 完成比例
        /// </summary>
        [JsonPropertyName("completion_ratio")]
        public float CompletionRatio { get; set; }

        [JsonPropertyName("description")] public string? Description { get; set; }

        [JsonPropertyName("tags")] public string? TagsRaw { get; set; }

        [JsonIgnore]
        public string[]? Tags => string.IsNullOrEmpty(TagsRaw)
            ? null
            : TagsRaw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        [JsonPropertyName("supported_endpoint_types")]
        public string[]? SupportedEndpointTypes { get; set; }

        /// <summary>
        /// 启用的分组集合
        /// </summary>
        [JsonPropertyName("enable_groups")]
        public List<string>? EnableGroups { get; set; }
    }
}

public class XiaoHuMiniModelMapping : XiaoAIModelMapping
{
    public override string Url { get; set; } = "https://xiaohumini.site/api/pricing_new";

    public XiaoHuMiniModelMapping() : base("XiaoHuMini")
    {
    }
}