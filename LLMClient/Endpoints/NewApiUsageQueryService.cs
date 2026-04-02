using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using LLMClient.Abstraction;
using LLMClient.Endpoints.Converters;
using LLMClient.Endpoints.OpenAIAPI;

namespace LLMClient.Endpoints;

public sealed class NewApiUsageQueryService
{
    private readonly HttpMessageHandler? _httpMessageHandler;

    public NewApiUsageQueryService()
    {
    }

    internal NewApiUsageQueryService(HttpMessageHandler httpMessageHandler)
    {
        _httpMessageHandler = httpMessageHandler;
    }

    public bool TryResolve(ILLMAPIEndpoint endpoint, out Uri? queryUri, out string reason)
    {
        queryUri = null;
        reason = string.Empty;

        if (endpoint is not APIEndPoint apiEndPoint)
        {
            reason = "当前仅支持 OpenAI Compatible 类型的终结点。";
            return false;
        }

        if (apiEndPoint.Option.ModelsSource != ModelSource.NewAPI)
        {
            reason = "当前终结点未配置为 NewAPI。";
            return false;
        }

        if (string.IsNullOrWhiteSpace(apiEndPoint.Option.NewApiSystemToken))
        {
            reason = "请先为 NewAPI 终结点填写系统令牌。";
            return false;
        }

        if (string.IsNullOrWhiteSpace(apiEndPoint.Option.NewApiUserId))
        {
            reason = "请先为 NewAPI 终结点填写用户 ID。";
            return false;
        }

        if (string.IsNullOrWhiteSpace(apiEndPoint.Option.ConfigOption.APIToken))
        {
            reason = "请先在终结点配置中填写 API Token。";
            return false;
        }

        try
        {
            queryUri = BuildTokenUsageUri(apiEndPoint.Option);
            return true;
        }
        catch (Exception ex)
        {
            reason = ex.Message;
            return false;
        }
    }

    public async Task<NewApiUsageSnapshot> QueryAsync(ILLMAPIEndpoint endpoint,
        CancellationToken cancellationToken = default)
    {
        if (endpoint is not APIEndPoint apiEndPoint)
        {
            throw new InvalidOperationException("当前终结点不支持 NewAPI 用量查询。");
        }

        if (!TryResolve(apiEndPoint, out var queryUri, out var reason) || queryUri == null)
        {
            throw new InvalidOperationException(reason);
        }

        using var httpClient = CreateHttpClient(apiEndPoint);
        using var request = new HttpRequestMessage(HttpMethod.Get, queryUri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiEndPoint.Option.NewApiSystemToken!.Trim());
        request.Headers.Add("new-api-user", apiEndPoint.Option.NewApiUserId!.Trim());
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(BuildErrorMessage(response, content));
        }

        using var document = JsonDocument.Parse(content);
        var snapshot = ParseTokenUsage(document.RootElement, apiEndPoint.Option.ConfigOption.APIToken, queryUri);
        if (snapshot == null)
        {
            throw new InvalidOperationException("NewAPI 返回内容缺少可识别的 token 用量信息。");
        }

        snapshot.QueryUrl = queryUri.ToString();
        snapshot.RefreshedAt = DateTimeOffset.Now;
        return snapshot;
    }

    internal static Uri BuildTokenUsageUri(APIEndPointOption option)
    {
        if (string.IsNullOrWhiteSpace(option.ConfigOption.URL))
        {
            throw new InvalidOperationException("请先在终结点配置中填写 API URL。"
            );
        }

        var rootUri = ResolveRootDomainUri(option);

        return new Uri(rootUri, "api/token/");
    }

    private HttpClient CreateHttpClient(APIEndPoint apiEndPoint)
    {
        if (_httpMessageHandler != null)
        {
            return new HttpClient(_httpMessageHandler, false);
        }

        var handler = apiEndPoint.Option.ConfigOption.ProxySetting.GetRealProxy().CreateHandler();
        handler.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
        return new HttpClient(handler, true);
    }

    private static Uri ResolveRootDomainUri(APIEndPointOption option)
    {
        var source = option.ConfigOption.URL;

        if (string.IsNullOrWhiteSpace(source) || !Uri.TryCreate(source, UriKind.Absolute, out var sourceUri))
        {
            throw new InvalidOperationException("API URL 不是合法的绝对地址。"
            );
        }

        return new UriBuilder(sourceUri)
        {
            Path = "/",
            Query = string.Empty,
            Fragment = string.Empty,
        }.Uri;
    }

    private static NewApiUsageSnapshot? ParseTokenUsage(JsonElement root, string configuredToken, Uri queryUri)
    {
        var tokens = ExtractTokenNodes(root).ToList();
        if (tokens.Count == 0)
        {
            return null;
        }

        JsonElement tokenNode;
        if (tokens.Count == 1)
        {
            tokenNode = tokens[0];
        }
        else
        {
            tokenNode = SelectMatchingToken(tokens, configuredToken, queryUri);
        }

        return MapSnapshot(tokenNode);
    }

    private static IEnumerable<JsonElement> ExtractTokenNodes(JsonElement root)
    {
        if (IsTokenNode(root))
        {
            yield return root.Clone();
            yield break;
        }

        if (root.ValueKind != JsonValueKind.Object)
        {
            yield break;
        }

        if (root.TryGetProperty("data", out var dataElement))
        {
            foreach (var item in ExtractTokenNodesFromData(dataElement))
            {
                yield return item;
            }
            yield break;
        }

        if (root.TryGetProperty("items", out var itemsElement))
        {
            foreach (var item in EnumerateArray(itemsElement))
            {
                if (IsTokenNode(item))
                {
                    yield return item.Clone();
                }
            }
        }
    }

    private static IEnumerable<JsonElement> ExtractTokenNodesFromData(JsonElement dataElement)
    {
        switch (dataElement.ValueKind)
        {
            case JsonValueKind.Object when IsTokenNode(dataElement):
                yield return dataElement.Clone();
                yield break;
            case JsonValueKind.Object:
                if (dataElement.TryGetProperty("items", out var itemsElement))
                {
                    foreach (var item in EnumerateArray(itemsElement))
                    {
                        if (IsTokenNode(item))
                        {
                            yield return item.Clone();
                        }
                    }
                }
                yield break;
            case JsonValueKind.Array:
                foreach (var item in dataElement.EnumerateArray())
                {
                    if (IsTokenNode(item))
                    {
                        yield return item.Clone();
                    }
                }
                yield break;
        }
    }

    private static IEnumerable<JsonElement> EnumerateArray(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        foreach (var item in element.EnumerateArray())
        {
            yield return item;
        }
    }

    private static bool IsTokenNode(JsonElement element)
    {
        return element.ValueKind == JsonValueKind.Object
               && (element.TryGetProperty("used_quota", out _)
                   || element.TryGetProperty("remain_quota", out _)
                   || element.TryGetProperty("quota", out _))
               && (element.TryGetProperty("key", out _)
                   || element.TryGetProperty("name", out _)
                   || element.TryGetProperty("id", out _));
    }

    private static JsonElement SelectMatchingToken(IReadOnlyList<JsonElement> tokens, string configuredToken, Uri queryUri)
    {
        var normalizedToken = configuredToken.Trim();
        var exactMatches = tokens.Where(token =>
                string.Equals(GetString(token, "key"), normalizedToken, StringComparison.Ordinal)
                || string.Equals(GetString(token, "token"), normalizedToken, StringComparison.Ordinal))
            .ToList();
        if (exactMatches.Count == 1)
        {
            return exactMatches[0];
        }

        var maskedMatches = tokens.Where(token =>
        {
            var key = GetString(token, "key") ?? GetString(token, "token");
            return MaskedKeyMatches(key, normalizedToken);
        }).ToList();
        if (maskedMatches.Count == 1)
        {
            return maskedMatches[0];
        }

        if (maskedMatches.Count > 1)
        {
            throw new InvalidOperationException("匹配到了多个 token，无法确定当前终结点对应哪一个。请确保当前 API Token 在返回结果中可唯一匹配。");
        }

        throw new InvalidOperationException($"在 token usage 接口返回结果中没有找到与当前 API Token 对应的记录：{queryUri}");
    }

    private static bool MaskedKeyMatches(string? maskedKey, string token)
    {
        if (string.IsNullOrWhiteSpace(maskedKey) || string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        var firstMaskIndex = maskedKey.IndexOf('*');
        if (firstMaskIndex < 0)
        {
            return string.Equals(maskedKey, token, StringComparison.Ordinal);
        }

        var lastMaskIndex = maskedKey.LastIndexOf('*');
        var prefix = maskedKey[..firstMaskIndex];
        var suffix = lastMaskIndex >= 0 && lastMaskIndex + 1 < maskedKey.Length
            ? maskedKey[(lastMaskIndex + 1)..]
            : string.Empty;

        if (!string.IsNullOrEmpty(prefix) && !token.StartsWith(prefix, StringComparison.Ordinal))
        {
            return false;
        }

        if (!string.IsNullOrEmpty(suffix) && !token.EndsWith(suffix, StringComparison.Ordinal))
        {
            return false;
        }

        return token.Length >= prefix.Length + suffix.Length;
    }

    private static NewApiUsageSnapshot MapSnapshot(JsonElement tokenNode)
    {
        return new NewApiUsageSnapshot
        {
            TokenId = GetInt32(tokenNode, "id"),
            UserId = GetInt32(tokenNode, "user_id"),
            TokenName = GetString(tokenNode, "name"),
            TokenKey = GetString(tokenNode, "key") ?? GetString(tokenNode, "token"),
            GroupName = GetString(tokenNode, "group"),
            IsEnabled = GetBoolean(tokenNode, "status"),
            UnlimitedQuota = GetBoolean(tokenNode, "unlimited_quota") == true,
            CreatedAt = GetDateTimeOffset(tokenNode, "created_time"),
            AccessedAt = GetDateTimeOffset(tokenNode, "accessed_time"),
            ExpiredAt = GetDateTimeOffset(tokenNode, "expired_time"),
            UsedQuota = GetDecimal(tokenNode, "used_quota"),
            RemainQuota = GetDecimal(tokenNode, "remain_quota"),
            Quota = ResolveQuota(tokenNode),
        };
    }

    private static decimal ResolveQuota(JsonElement tokenNode)
    {
        var directQuota = GetDecimal(tokenNode, "quota");
        if (directQuota > 0)
        {
            return directQuota;
        }

        if (GetBoolean(tokenNode, "unlimited_quota") == true)
        {
            return 0;
        }

        return GetDecimal(tokenNode, "used_quota") + GetDecimal(tokenNode, "remain_quota");
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        return property.ValueKind switch
        {
            JsonValueKind.String => property.GetString(),
            JsonValueKind.Number => property.GetRawText(),
            JsonValueKind.True => bool.TrueString,
            JsonValueKind.False => bool.FalseString,
            _ => null,
        };
    }

    private static int? GetInt32(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        if (property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out var intValue))
        {
            return intValue;
        }

        if (property.ValueKind == JsonValueKind.String && int.TryParse(property.GetString(), out intValue))
        {
            return intValue;
        }

        return null;
    }

    private static bool? GetBoolean(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        if (property.ValueKind == JsonValueKind.True)
        {
            return true;
        }

        if (property.ValueKind == JsonValueKind.False)
        {
            return false;
        }

        if (property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out var numeric))
        {
            return numeric != 0;
        }

        if (property.ValueKind == JsonValueKind.String)
        {
            var stringValue = property.GetString();
            if (bool.TryParse(stringValue, out var boolValue))
            {
                return boolValue;
            }

            if (int.TryParse(stringValue, out numeric))
            {
                return numeric != 0;
            }
        }

        return null;
    }

    private static decimal GetDecimal(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return 0;
        }

        if (property.ValueKind == JsonValueKind.Number && property.TryGetDecimal(out var decimalValue))
        {
            return decimalValue;
        }

        if (property.ValueKind == JsonValueKind.String && decimal.TryParse(property.GetString(), out decimalValue))
        {
            return decimalValue;
        }

        return 0;
    }

    private static DateTimeOffset? GetDateTimeOffset(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        if (property.ValueKind == JsonValueKind.String && DateTimeOffset.TryParse(property.GetString(), out var dateTimeOffset))
        {
            return dateTimeOffset;
        }

        if (property.ValueKind == JsonValueKind.Number && property.TryGetInt64(out var unixValue))
        {
            if (unixValue < 0)
            {
                return DateTimeOffset.FromUnixTimeSeconds(0);
            }

            return unixValue > 10_000_000_000
                ? DateTimeOffset.FromUnixTimeMilliseconds(unixValue)
                : DateTimeOffset.FromUnixTimeSeconds(unixValue);
        }

        return null;
    }

    private static string BuildErrorMessage(HttpResponseMessage response, string content)
    {
        var statusText = $"NewAPI 请求失败 ({(int)response.StatusCode} {response.ReasonPhrase})";
        if (string.IsNullOrWhiteSpace(content))
        {
            return statusText;
        }

        try
        {
            var error = JsonSerializer.Deserialize<NewApiErrorResponse>(content);
            if (!string.IsNullOrWhiteSpace(error?.Message))
            {
                return $"{statusText}: {error.Message}";
            }
        }
        catch
        {
            // ignore parse failures and fall back to raw content
        }

        return $"{statusText}: {content}";
    }


    private sealed class NewApiErrorResponse
    {
        [JsonPropertyName("msg")]
        public string? Message { get; set; }
    }
}

