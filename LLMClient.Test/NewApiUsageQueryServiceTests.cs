using System.Net;
using System.Text;
using LLMClient.Abstraction;
using LLMClient.Endpoints;
using LLMClient.Endpoints.Converters;
using LLMClient.Endpoints.OpenAIAPI;
using Microsoft.Extensions.Logging;

namespace LLMClient.Test;

public class NewApiUsageQueryServiceTests
{
    [Fact]
    public void ResolveNewApiPricingUrl_BuildsApiPricing_FromApiUrlRoot()
    {
        var pricingUrl = APIEndPointOption.ResolveNewApiPricingUrl("https://xiaohumini.site/v1");

        Assert.Equal("https://xiaohumini.site/api/pricing", pricingUrl);
    }

    [Fact]
    public void ResolveNewApiPricingUrl_StripsAdditionalPathSegments()
    {
        var pricingUrl = APIEndPointOption.ResolveNewApiPricingUrl("https://xiaohumini.site/openai/v1/chat/completions");

        Assert.Equal("https://xiaohumini.site/api/pricing", pricingUrl);
    }

    [Fact]
    public void TryResolve_BuildsTokenUsageUri_FromConfiguredV1Url()
    {
        TestFixture.RunInStaThread(() =>
        {
            var endpoint = CreateEndpoint("https://demo.newapi.ai/v1", "token-123", ModelSource.NewAPI);
            var service = new NewApiUsageQueryService();

            var supported = service.TryResolve(endpoint, out var queryUri, out var reason);

            Assert.True(supported, reason);
            Assert.Equal(new Uri("https://demo.newapi.ai/api/token/"), queryUri);
        });
    }

    [Fact]
    public void TryResolve_StripsExtraPath_AndUsesRootDomainPlusMethodPath()
    {
        TestFixture.RunInStaThread(() =>
        {
            var endpoint = CreateEndpoint(
                "https://demo.newapi.ai/openai/v1",
                "token-123",
                ModelSource.NewAPI,
                "https://demo.newapi.ai/dashboard/api/token/42");
            var service = new NewApiUsageQueryService();

            var supported = service.TryResolve(endpoint, out var queryUri, out var reason);

            Assert.True(supported, reason);
            Assert.Equal(new Uri("https://demo.newapi.ai/api/token/"), queryUri);
        });
    }

    [Fact]
    public void TryResolve_IgnoresApiLogUrl_AndStillUsesApiUrlRootDomain()
    {
        TestFixture.RunInStaThread(() =>
        {
            var endpoint = CreateEndpoint(
                "https://demo.newapi.ai/v1",
                "token-123",
                ModelSource.NewAPI,
                "https://manage.newapi.ai/api/token/42");
            var service = new NewApiUsageQueryService();

            var supported = service.TryResolve(endpoint, out var queryUri, out var reason);

            Assert.True(supported, reason);
            Assert.Equal(new Uri("https://demo.newapi.ai/api/token/"), queryUri);
        });
    }

    [Fact]
    public async Task QueryAsync_SendsSystemTokenAndUserIdHeaders_AndParsesPagedItemsPayload()
    {
        await Task.Run(() => TestFixture.RunInStaThread(() =>
        {
            var handler = new HttpMessageHandlerStub
            {
                ResponseToReturn = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        """
                        {
                          "data": {
                            "page": 1,
                            "page_size": 10,
                            "start_timestamp": 0,
                            "end_timestamp": 0,
                            "total": 1,
                            "items": [
                              {
                                "id": 7,
                                "user_id": 124612,
                                "name": "production",
                                "key": "sk-abc********xyz",
                                "status": 1,
                                "created_time": 1773306486,
                                "accessed_time": 1773970687,
                                "expired_time": -1,
                                "remain_quota": 75.25,
                                "unlimited_quota": false,
                                "used_quota": 45.25,
                                "group": "Claude Code专属"
                              }
                            ]
                          }
                        }
                        """,
                        Encoding.UTF8,
                        "application/json")
                }
            };
            var endpoint = CreateEndpoint("https://demo.newapi.ai/v1/", "sk-abcdefghijklmnopqrstuvwxyzxyz", ModelSource.NewAPI,
                "https://manage.newapi.ai/api/token/7");
            var service = new NewApiUsageQueryService(handler);

            var result = service.QueryAsync(endpoint).GetAwaiter().GetResult();

            Assert.Equal(HttpMethod.Get, handler.Method);
            Assert.Equal(new Uri("https://demo.newapi.ai/api/token/"), handler.RequestUri);
            Assert.Equal("Bearer", handler.RequestHeaders?.Authorization?.Scheme);
            Assert.Equal("system-token-123", handler.RequestHeaders?.Authorization?.Parameter);
            Assert.Equal("124612", handler.RequestHeaders?.GetValues("new-api-user").Single());
            Assert.Equal(120.5m, result.Quota);
            Assert.Equal(45.25m, result.UsedQuota);
            Assert.Equal(75.25m, result.RemainQuota);
            Assert.Equal(7, result.TokenId);
            Assert.Equal(124612, result.UserId);
            Assert.Equal("production", result.TokenName);
            Assert.Equal("sk-abc********xyz", result.TokenKey);
            Assert.Equal("Claude Code专属", result.GroupName);
            Assert.True(result.IsEnabled);
            Assert.False(result.UnlimitedQuota);
            Assert.Equal("https://demo.newapi.ai/api/token/", result.QueryUrl);
            Assert.InRange(result.UsedPercent, 37.55, 37.56);
        }));
    }

    [Fact]
    public async Task QueryAsync_MatchesCurrentToken_FromTokenListPayload()
    {
        await Task.Run(() => TestFixture.RunInStaThread(() =>
        {
            var handler = new HttpMessageHandlerStub
            {
                ResponseToReturn = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        """
                        {
                          "data": {
                            "page": 1,
                            "page_size": 10,
                            "start_timestamp": 0,
                            "end_timestamp": 0,
                            "total": 2,
                            "items": [
                              {
                                "id": 1,
                                "user_id": 124612,
                                "name": "other",
                                "key": "sk-foo********bar",
                                "used_quota": 1,
                                "remain_quota": 9,
                                "status": 1,
                                "unlimited_quota": false,
                                "group": "Other"
                              },
                              {
                                "id": 2,
                                "user_id": 124612,
                                "name": "current-token",
                                "key": "sk-reHR**********OspA",
                                "used_quota": 810027,
                                "remain_quota": 4999189973,
                                "status": 1,
                                "created_time": 1770812415,
                                "accessed_time": 1773307327,
                                "expired_time": -1,
                                "unlimited_quota": false,
                                "group": "Codex专属"
                              }
                            ]
                          },
                          "message": "",
                          "success": true
                        }
                        """,
                        Encoding.UTF8,
                        "application/json")
                }
            };
            var endpoint = CreateEndpoint("https://demo.newapi.ai/v1/", "sk-reHR1234567890OspA", ModelSource.NewAPI);
            var service = new NewApiUsageQueryService(handler);

            var result = service.QueryAsync(endpoint).GetAwaiter().GetResult();

            Assert.Equal(new Uri("https://demo.newapi.ai/api/token/"), handler.RequestUri);
            Assert.Equal(2, result.TokenId);
            Assert.Equal(124612, result.UserId);
            Assert.Equal("current-token", result.TokenName);
            Assert.Equal("sk-reHR**********OspA", result.TokenKey);
            Assert.True(result.IsEnabled);
            Assert.Equal(5000000000m, result.Quota);
            Assert.Equal(810027m, result.UsedQuota);
            Assert.Equal(4999189973m, result.RemainQuota);
            Assert.Equal("Codex专属", result.GroupName);
            Assert.False(result.UnlimitedQuota);
        }));
    }

    [Fact]
    public void TryResolve_RejectsEndpointWithoutNewApiHints()
    {
        TestFixture.RunInStaThread(() =>
        {
            var endpoint = CreateEndpoint("https://api.example.com/v1", "token-123", ModelSource.None);
            var service = new NewApiUsageQueryService();

            var supported = service.TryResolve(endpoint, out var queryUri, out var reason);

            Assert.False(supported);
            Assert.Null(queryUri);
            Assert.Contains("未配置为 NewAPI", reason);
        });
    }

    [Fact]
    public void TryResolve_RejectsMissingApiUrl_EvenIfApiLogUrlExists()
    {
        TestFixture.RunInStaThread(() =>
        {
            var endpoint = CreateEndpoint(string.Empty, "token-123", ModelSource.NewAPI,
                "https://manage.newapi.ai/api/token/42");
            var service = new NewApiUsageQueryService();

            var supported = service.TryResolve(endpoint, out var queryUri, out var reason);

            Assert.False(supported);
            Assert.Null(queryUri);
            Assert.Contains("API URL", reason);
        });
    }

    [Fact]
    public void TryResolve_RejectsMissingSystemToken()
    {
        TestFixture.RunInStaThread(() =>
        {
            var endpoint = CreateEndpoint("https://demo.newapi.ai/v1", "token-123", ModelSource.NewAPI,
                systemToken: string.Empty);
            var service = new NewApiUsageQueryService();

            var supported = service.TryResolve(endpoint, out var queryUri, out var reason);

            Assert.False(supported);
            Assert.Null(queryUri);
            Assert.Contains("系统令牌", reason);
        });
    }

    [Fact]
    public void TryResolve_RejectsMissingUserId()
    {
        TestFixture.RunInStaThread(() =>
        {
            var endpoint = CreateEndpoint("https://demo.newapi.ai/v1", "token-123", ModelSource.NewAPI,
                userId: string.Empty);
            var service = new NewApiUsageQueryService();

            var supported = service.TryResolve(endpoint, out var queryUri, out var reason);

            Assert.False(supported);
            Assert.Null(queryUri);
            Assert.Contains("用户 ID", reason);
        });
    }

    private static APIEndPoint CreateEndpoint(string url, string token, ModelSource modelSource, string? apiLogUrl = null,
        string? systemToken = "system-token-123", string? userId = "124612")
    {
        var option = new APIEndPointOption
        {
            DisplayName = "NewAPI Test",
            IconUrl = "https://example.com/icon.png",
            ModelsSource = modelSource,
            ApiLogUrl = apiLogUrl,
            NewApiSystemToken = systemToken,
            NewApiUserId = userId,
            ConfigOption = new APIDefaultOption
            {
                URL = url,
                APIToken = token,
            }
        };

        return new APIEndPoint(option, LoggerFactory.Create(_ => { }), new DefaultTokensCounter());
    }
}


