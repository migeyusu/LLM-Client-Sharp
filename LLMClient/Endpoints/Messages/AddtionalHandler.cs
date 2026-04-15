using System.Net.Http;
using LLMClient.Component.Utility;
using LLMClient.Endpoints;
using Microsoft.CodeAnalysis.Operations;

public class AddtionalHandler : DelegatingHandler
{
    private readonly IDictionary<string, string> _additionalHttpHeader;

    public AddtionalHandler(HttpMessageHandler innerHandler, IDictionary<string, string> additionalHttpHeader)
        : base(innerHandler)
    {
        this._additionalHttpHeader = additionalHttpHeader;
    }


    private const string UserAgentHeaderName = "User-Agent";

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (_additionalHttpHeader.TryGetValue(UserAgentHeaderName, out var userAgentValue))
        {
            request.Headers.UserAgent.Clear();
            request.Headers.UserAgent.ParseAdd(userAgentValue);
            _additionalHttpHeader.Remove(UserAgentHeaderName);
        }

        foreach (var (key, value) in _additionalHttpHeader)
        {
            request.Headers.TryAddWithoutValidation(key, value);
        }

        return base.SendAsync(request, cancellationToken);
    }
}