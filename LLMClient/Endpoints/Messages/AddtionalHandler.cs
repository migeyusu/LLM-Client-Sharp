using System.Net.Http;

public class AddtionalHandler : DelegatingHandler
{
    private readonly IReadOnlyDictionary<string, string> _additionalHttpHeader;

    public AddtionalHandler(HttpMessageHandler innerHandler, IReadOnlyDictionary<string, string> additionalHttpHeader)
        : base(innerHandler)
    {
        this._additionalHttpHeader = additionalHttpHeader;
    }


    private const string UserAgentHeaderName = "User-Agent";

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        foreach (var (key, value) in _additionalHttpHeader)
        {
            if (key.Equals(UserAgentHeaderName, StringComparison.Ordinal))
            {
                request.Headers.UserAgent.Clear();
                request.Headers.UserAgent.ParseAdd(value);
            }
            else
            {
                request.Headers.TryAddWithoutValidation(key, value);
            }
        }

        return base.SendAsync(request, cancellationToken);
    }
}