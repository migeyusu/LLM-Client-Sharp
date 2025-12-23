using System.Net.Http.Headers;
using System.Text;

namespace LLMClient.Test;

internal sealed class HttpMessageHandlerStub : DelegatingHandler
{
    public HttpRequestHeaders? RequestHeaders { get; private set; }

    public HttpContentHeaders? ContentHeaders { get; private set; }

    public byte[]? RequestContent { get; private set; }

    public Uri? RequestUri { get; private set; }

    public HttpMethod? Method { get; private set; }

    public HttpResponseMessage ResponseToReturn { get; set; }

    public HttpMessageHandlerStub() : base(new HttpClientHandler())
    {
        this.ResponseToReturn = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json")
        };
    }

    protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        this.Method = request.Method;
        this.RequestUri = request.RequestUri;
        this.RequestHeaders = request.Headers;
        if (request.Content is not null)
        {
#pragma warning disable CA2016 // Forward the 'CancellationToken' parameter to methods; overload doesn't exist on .NET Framework
            this.RequestContent = request.Content.ReadAsByteArrayAsync(cancellationToken).Result;
#pragma warning restore CA2016
        }

        this.ContentHeaders = request.Content?.Headers;
        return this.ResponseToReturn;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        this.Method = request.Method;
        this.RequestUri = request.RequestUri;
        this.RequestHeaders = request.Headers;
        if (request.Content is not null)
        {
#pragma warning disable CA2016 // Forward the 'CancellationToken' parameter to methods; overload doesn't exist on .NET Framework
            this.RequestContent = await request.Content.ReadAsByteArrayAsync();
#pragma warning restore CA2016
        }

        this.ContentHeaders = request.Content?.Headers;

        return await Task.FromResult(this.ResponseToReturn);
    }
}