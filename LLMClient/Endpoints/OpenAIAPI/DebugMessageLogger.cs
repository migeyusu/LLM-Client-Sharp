using System.Diagnostics;
using System.Net.Http;

namespace LLMClient.Endpoints.OpenAIAPI;

public class DebugMessageLogger : DelegatingHandler
{
    public DebugMessageLogger() : base(new HttpClientHandler())
    {
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var requestContent = request.Content;
        if (requestContent != null)
        {
            var requestString = await requestContent.ReadAsStringAsync(cancellationToken);
            /*var jsonNode = JsonNode.Parse(requestString);
            var foo = new[]
            {
                new
                {
                    id = "web",
                }
            };
            var node = JsonSerializer.SerializeToNode(foo);
            jsonNode["plugins"] = node;
            request.Content = new StringContent(JsonSerializer.Serialize(jsonNode));*/
        }

        var httpResponseMessage = await base.SendAsync(request, cancellationToken);
        var response = await httpResponseMessage.Content.ReadAsStringAsync(cancellationToken);
        Debug.WriteLine(response);
        return httpResponseMessage;
    }
}