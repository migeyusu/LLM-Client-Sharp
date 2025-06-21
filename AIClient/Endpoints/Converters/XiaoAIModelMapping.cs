using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using LLMClient.Endpoints.OpenAIAPI;
using LLMClient.UI.Component;

namespace LLMClient.Endpoints.Converters;

public class XiaoAIModelMapping : ModelMapping
{
    public XiaoAIModelMapping() : base("XiaoAI")
    {
    }

    public override IList<string> AvailableModels
    {
        get { return priceInfos.Keys.ToArray(); }
    }

    Dictionary<string, ModelDetails> priceInfos = new Dictionary<string, ModelDetails>();

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
                           new HttpRequestMessage(HttpMethod.Get, "https://xiaoai.plus/api/pricing"))
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

                            if (listNode == null)
                            {
                                return false;
                            }

                            var modelDetails = listNode.Deserialize<ModelDetails[]>();
                            if (modelDetails == null)
                            {
                                return false;
                            }

                            this.priceInfos = modelDetails.ToDictionary(modelDetail => modelDetail.ModelName);
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
        if (priceInfos.TryGetValue(modelInfoId, out var modelDetails))
        {
            if (modelInfo.PriceCalculator is TokenBasedPriceCalculator calculator)
            {
                calculator.DiscountFactor = 1;
                calculator.InputPrice = modelDetails.ModelRatio * 2;
                calculator.OutputPrice = modelDetails.ModelRatio * 2 * modelDetails.CompletionRatio;
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

        /// <summary>
        /// 启用的分组集合
        /// </summary>
        [JsonPropertyName("enable_groups")]
        public List<string>? EnableGroups { get; set; }
    }
}