using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using LLMClient.Endpoints.OpenAIAPI;
using LLMClient.UI.Component.Utility;

namespace LLMClient.Endpoints.Converters;

public class OpenRouterModelMapping : ModelMapping
{
    public OpenRouterModelMapping() : base("OpenRouter")
    {
    }

    public override IList<string> AvailableModels => _modelInfos.Select((model => model.ShortName)).ToArray();

    private OpenRouterModel[] _modelInfos = [];

    /*const string inputModalities = "text";

    const string outputModalities = "text";*/

    public override async Task<bool> Refresh()
    {
        var httpClient = new HttpClient();
        try
        {
            var responseMessage =
                await httpClient.GetAsync(new Uri("https://openrouter.ai/api/frontend/models"));
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

                var openRouterModels = listNode.Deserialize<OpenRouterModel[]>();
                if (openRouterModels != null)
                {
                    _modelInfos = openRouterModels
                        .Where((info => info.Endpoint is { IsHidden: false } &&
                                        !string.IsNullOrEmpty(info.Slug) && !string.IsNullOrEmpty(info.ShortName)))
                        .ToArray();
                    return true;
                }
            }
        }
        catch (Exception e)
        {
            MessageEventBus.Publish($"Error fetching models from OpenRouter: {e.Message}");
        }
        finally
        {
            httpClient.Dispose();
        }

        return false;
    }

    public override APIModelInfo? TryGet(string modelId)
    {
        var openRouterModel = _modelInfos.FirstOrDefault((model => model.Slug == modelId));
        if (openRouterModel == null)
        {
            return null;
        }

        var apiModelInfo = new APIModelInfo
        {
            Id = openRouterModel.Slug,
            Name = openRouterModel.ShortName,
        };
        this.MapInfo(apiModelInfo);
        return apiModelInfo;
    }

    private const string BaseUrl = "https://openrouter.ai";

    public override bool MapInfo(APIModelInfo modelInfo)
    {
        var openRouterModel = _modelInfos.FirstOrDefault((model =>
        {
            var id = modelInfo.Id;
            return model.Slug == id
                   || model.Endpoint?.ModelVariantSlug == id;
        }));
        if (openRouterModel == null)
        {
            return false;
        }

        var endpoint = openRouterModel.Endpoint;
        Trace.Assert(endpoint != null, "Endpoint should not be null");
        modelInfo.Description = openRouterModel.Description;
        modelInfo.MaxContextSize = openRouterModel.ContextLength ?? 1000000;
        var maxTokenLimit = endpoint.MaxCompletionTokens;
        if (maxTokenLimit != null)
        {
            modelInfo.MaxTokensEnable = true;
            modelInfo.MaxTokenLimit = maxTokenLimit.Value;
        }

        var iconUrl = endpoint.ProviderInfo?.Icon?.Url;
        if (!string.IsNullOrEmpty(iconUrl))
        {
            if (Uri.TryCreate(iconUrl, UriKind.RelativeOrAbsolute, out var uri))
            {
                modelInfo.UrlIconEnable = true;
                modelInfo.IconUrl = uri.IsAbsoluteUri ? iconUrl : BaseUrl + iconUrl;
            }
        }

        modelInfo.SupportStreaming = endpoint.CanAbort == true;
        modelInfo.Reasonable = endpoint.SupportsReasoning == true;
        modelInfo.SupportFunctionCall = endpoint.SupportsToolParameters == true;
        var list = endpoint.SupportedParameters;
        if (list != null)
        {
            modelInfo.TemperatureEnable = list.Contains("temperature");
            modelInfo.TopPEnable = list.Contains("top_p");
            modelInfo.TopKEnable = list.Contains("top_k");
            modelInfo.FrequencyPenaltyEnable = list.Contains("frequency_penalty");
            modelInfo.PresencePenaltyEnable = list.Contains("presence_penalty");
            modelInfo.SeedEnable = list.Contains("seed");
            if (endpoint.MaxCompletionTokens == null)
            {
                modelInfo.MaxTokensEnable = list.Contains("max_tokens");
            }

            modelInfo.SupportSearch = list.Contains("web_search_options");
        }

        var inputModalities = openRouterModel.InputModalities;
        if (inputModalities != null)
        {
            modelInfo.SupportImageInput = inputModalities.Contains("image");
            modelInfo.SupportVideoInput = inputModalities.Contains("video");
        }

        var outputModalities = openRouterModel.OutputModalities;
        if (outputModalities != null)
        {
            modelInfo.SupportTextGeneration = outputModalities.Contains("text");
            modelInfo.SupportImageGeneration = outputModalities.Contains("image");
        }
        
        if (modelInfo.PriceCalculator is TokenBasedPriceCalculator tokenBasedPriceCalculator)
        {
            if (endpoint.IsFree == true)
            {
                tokenBasedPriceCalculator.DiscountFactor = 0;
            }
            else
            {
                var pricing = endpoint.Pricing;
                if (pricing != null)
                {
                    tokenBasedPriceCalculator.DiscountFactor = 1;
                    if (double.TryParse(pricing.Prompt, out var input))
                    {
                        tokenBasedPriceCalculator.InputPrice = input * 1000000;
                    }

                    if (double.TryParse(pricing.Completion, out var output))
                    {
                        tokenBasedPriceCalculator.OutputPrice = output * 1000000;
                    }
                }
                else
                {
                    tokenBasedPriceCalculator.Enable = false;
                }
            }
        }

        return true;
    }


    public class OpenRouterModel
    {
        [JsonPropertyName("slug")] public string Slug { get; set; } = string.Empty;

        [JsonPropertyName("hf_slug")] public string? HfSlug { get; set; }

        [JsonPropertyName("updated_at")] public string? UpdatedAt { get; set; }

        [JsonPropertyName("created_at")] public string? CreatedAt { get; set; }

        [JsonPropertyName("hf_updated_at")] public string? HfUpdatedAt { get; set; }

        [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;

        [JsonPropertyName("short_name")] public string ShortName { get; set; } = string.Empty;

        [JsonPropertyName("author")] public string? Author { get; set; }

        [JsonPropertyName("description")] public string? Description { get; set; }

        [JsonPropertyName("model_version_group_id")]
        public string? ModelVersionGroupId { get; set; }

        [JsonPropertyName("context_length")] public int? ContextLength { get; set; }

        [JsonPropertyName("input_modalities")] public List<string>? InputModalities { get; set; }

        [JsonPropertyName("output_modalities")]
        public List<string>? OutputModalities { get; set; }

        [JsonPropertyName("has_text_output")] public bool? HasTextOutput { get; set; }

        [JsonPropertyName("group")] public string? Group { get; set; }

        [JsonPropertyName("instruct_type")] public string? InstructType { get; set; }

        [JsonPropertyName("default_system")] public string? DefaultSystem { get; set; }

        [JsonPropertyName("default_stops")] public List<string>? DefaultStops { get; set; }

        [JsonPropertyName("hidden")] public bool? Hidden { get; set; }

        [JsonPropertyName("router")] public string? Router { get; set; }

        [JsonPropertyName("warning_message")] public string? WarningMessage { get; set; }

        [JsonPropertyName("permaslug")] public string? Permaslug { get; set; }

        [JsonPropertyName("reasoning_config")] public object? ReasoningConfig { get; set; }

        /// <summary>
        /// reasoning config，暂时不需要
        /// </summary>
        [JsonPropertyName("features")]
        public Dictionary<string, object>? Features { get; set; }

        [JsonPropertyName("endpoint")] public Endpoint? Endpoint { get; set; }
    }

    public class VariablePricing
    {
        [JsonPropertyName("type")] public string? Type { get; set; }

        [JsonPropertyName("threshold")] public object? Threshold { get; set; }

        [JsonPropertyName("prompt")] public string? Prompt { get; set; }

        [JsonPropertyName("completions")] public string? Completions { get; set; }

        [JsonPropertyName("input_cache_read")] public string? InputCacheRead { get; set; }

        [JsonPropertyName("input_cache_write")]
        public string? InputCacheWrite { get; set; }
    }

    public class Endpoint
    {
        [JsonPropertyName("id")] public string? Id { get; set; }

        [JsonPropertyName("name")] public string? Name { get; set; }

        [JsonPropertyName("context_length")] public int? ContextLength { get; set; }

        [JsonPropertyName("model")] public ModelDetails? Model { get; set; }

        [JsonPropertyName("model_variant_slug")]
        public string? ModelVariantSlug { get; set; }

        [JsonPropertyName("model_variant_permaslug")]
        public string? ModelVariantPermaslug { get; set; }

        [JsonPropertyName("provider_name")] public string? ProviderName { get; set; }

        [JsonPropertyName("provider_info")] public ProviderInfo? ProviderInfo { get; set; }

        [JsonPropertyName("provider_display_name")]
        public string? ProviderDisplayName { get; set; }

        [JsonPropertyName("provider_slug")] public string? ProviderSlug { get; set; }

        [JsonPropertyName("provider_model_id")]
        public string? ProviderModelId { get; set; }

        [JsonPropertyName("quantization")] public string? Quantization { get; set; }

        [JsonPropertyName("variant")] public string? Variant { get; set; }

        [JsonPropertyName("is_free")] public bool? IsFree { get; set; }

        [JsonPropertyName("can_abort")] public bool? CanAbort { get; set; }

        [JsonPropertyName("max_prompt_tokens")]
        public int? MaxPromptTokens { get; set; }

        [JsonPropertyName("max_completion_tokens")]
        public int? MaxCompletionTokens { get; set; }

        [JsonPropertyName("max_prompt_images")]
        public int? MaxPromptImages { get; set; }

        [JsonPropertyName("max_tokens_per_image")]
        public int? MaxTokensPerImage { get; set; }

        [JsonPropertyName("supported_parameters")]
        public List<string>? SupportedParameters { get; set; }

        [JsonPropertyName("is_byok")] public bool? IsByok { get; set; }

        [JsonPropertyName("moderation_required")]
        public bool? ModerationRequired { get; set; }

        [JsonPropertyName("data_policy")] public DataPolicy? DataPolicy { get; set; }

        [JsonPropertyName("pricing")] public Pricing? Pricing { get; set; }

        [JsonPropertyName("variable_pricings")]
        public VariablePricing[]? VariablePricings { get; set; }

        [JsonPropertyName("is_hidden")] public bool? IsHidden { get; set; }

        [JsonPropertyName("is_deranked")] public bool? IsDeranked { get; set; }

        [JsonPropertyName("is_disabled")] public bool? IsDisabled { get; set; }

        [JsonPropertyName("supports_tool_parameters")]
        public bool? SupportsToolParameters { get; set; }

        [JsonPropertyName("supports_reasoning")]
        public bool? SupportsReasoning { get; set; }

        [JsonPropertyName("supports_multipart")]
        public bool? SupportsMultipart { get; set; }

        [JsonPropertyName("limit_rpm")] public int? LimitRpm { get; set; }

        [JsonPropertyName("limit_rpd")] public int? LimitRpd { get; set; }

        [JsonPropertyName("limit_rpm_cf")] public int? LimitRpmCf { get; set; }

        [JsonPropertyName("has_completions")] public bool? HasCompletions { get; set; }

        [JsonPropertyName("has_chat_completions")]
        public bool? HasChatCompletions { get; set; }

        [JsonPropertyName("features")] public EndpointFeatures? Features { get; set; }

        [JsonPropertyName("provider_region")] public string? ProviderRegion { get; set; }
    }

    public class ModelDetails
    {
        [JsonPropertyName("slug")] public string? Slug { get; set; }

        [JsonPropertyName("hf_slug")] public string? HfSlug { get; set; }

        [JsonPropertyName("updated_at")] public string? UpdatedAt { get; set; }

        [JsonPropertyName("created_at")] public string? CreatedAt { get; set; }

        [JsonPropertyName("hf_updated_at")] public string? HfUpdatedAt { get; set; }

        [JsonPropertyName("name")] public string? Name { get; set; }

        [JsonPropertyName("short_name")] public string? ShortName { get; set; }

        [JsonPropertyName("author")] public string? Author { get; set; }

        [JsonPropertyName("description")] public string? Description { get; set; }

        [JsonPropertyName("model_version_group_id")]
        public string? ModelVersionGroupId { get; set; }

        [JsonPropertyName("context_length")] public int? ContextLength { get; set; }

        [JsonPropertyName("input_modalities")] public List<string>? InputModalities { get; set; }

        [JsonPropertyName("output_modalities")]
        public List<string>? OutputModalities { get; set; }

        [JsonPropertyName("has_text_output")] public bool? HasTextOutput { get; set; }

        [JsonPropertyName("group")] public string? Group { get; set; }

        [JsonPropertyName("instruct_type")] public string? InstructType { get; set; }

        [JsonPropertyName("default_system")] public string? DefaultSystem { get; set; }

        [JsonPropertyName("default_stops")] public List<string>? DefaultStops { get; set; }

        [JsonPropertyName("hidden")] public bool? Hidden { get; set; }

        [JsonPropertyName("router")] public string? Router { get; set; }

        [JsonPropertyName("warning_message")] public string? WarningMessage { get; set; }

        [JsonPropertyName("permaslug")] public string? Permaslug { get; set; }

        [JsonPropertyName("reasoning_config")] public object? ReasoningConfig { get; set; }

        [JsonPropertyName("features")] public ModelFeatures? Features { get; set; }
    }

    public class ModelFeatures
    {
        [JsonPropertyName("supported_parameters")]
        public Dictionary<string, object>? SupportedParameters { get; set; }

        [JsonPropertyName("supports_document_url")]
        public object? SupportsDocumentUrl { get; set; }
    }


    public class ProviderInfo
    {
        [JsonPropertyName("name")] public string? Name { get; set; }

        [JsonPropertyName("displayName")] public string? DisplayName { get; set; }

        [JsonPropertyName("slug")] public string? Slug { get; set; }

        [JsonPropertyName("baseUrl")] public string? BaseUrl { get; set; }

        [JsonPropertyName("dataPolicy")] public DataPolicy? DataPolicy { get; set; }

        [JsonPropertyName("headquarters")] public string? Headquarters { get; set; }

        [JsonPropertyName("hasChatCompletions")]
        public bool? HasChatCompletions { get; set; }

        [JsonPropertyName("hasCompletions")] public bool? HasCompletions { get; set; }

        [JsonPropertyName("isAbortable")] public bool? IsAbortable { get; set; }

        [JsonPropertyName("moderationRequired")]
        public bool? ModerationRequired { get; set; }

        [JsonPropertyName("editors")] public List<object>? Editors { get; set; }

        [JsonPropertyName("owners")] public List<object>? Owners { get; set; }

        [JsonPropertyName("isMultipartSupported")]
        public bool? IsMultipartSupported { get; set; }

        [JsonPropertyName("statusPageUrl")] public string? StatusPageUrl { get; set; }

        [JsonPropertyName("byokEnabled")] public bool? ByokEnabled { get; set; }

        [JsonPropertyName("icon")] public Icon? Icon { get; set; }
    }

    public class DataPolicy
    {
        [JsonPropertyName("termsOfServiceURL")]
        public string? TermsOfServiceURL { get; set; }

        [JsonPropertyName("privacyPolicyURL")] public string? PrivacyPolicyURL { get; set; }

        [JsonPropertyName("paidModels")] public PaidModels? PaidModels { get; set; }

        [JsonPropertyName("requiresUserIDs")] public bool? RequiresUserIDs { get; set; }

        [JsonPropertyName("training")] public bool? Training { get; set; }

        [JsonPropertyName("retainsPrompts")] public bool? RetainsPrompts { get; set; }

        [JsonPropertyName("retentionDays")] public int? RetentionDays { get; set; }
    }

    public class PaidModels
    {
        [JsonPropertyName("training")] public bool? Training { get; set; }

        [JsonPropertyName("retainsPrompts")] public bool? RetainsPrompts { get; set; }

        [JsonPropertyName("retentionDays")] public int? RetentionDays { get; set; }
    }

    public class Icon
    {
        [JsonPropertyName("url")] public string? Url { get; set; }
    }

    public class Pricing
    {
        [JsonPropertyName("prompt")] public string? Prompt { get; set; }

        [JsonPropertyName("completion")] public string? Completion { get; set; }

        [JsonPropertyName("image")] public string? Image { get; set; }

        [JsonPropertyName("request")] public string? Request { get; set; }

        [JsonPropertyName("input_cache_read")] public string? InputCacheRead { get; set; }

        [JsonPropertyName("input_cache_write")]
        public string? InputCacheWrite { get; set; }

        [JsonPropertyName("web_search")] public string? WebSearch { get; set; }

        [JsonPropertyName("internal_reasoning")]
        public string? InternalReasoning { get; set; }

        [JsonPropertyName("discount")] public double? Discount { get; set; }
    }

    public class EndpointFeatures
    {
        [JsonPropertyName("supported_parameters")]
        public Dictionary<string, object>? SupportedParameters { get; set; }

        [JsonPropertyName("supports_document_url")]
        public string? SupportsDocumentUrl { get; set; }
    }
}