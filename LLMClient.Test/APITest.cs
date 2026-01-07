using System.ClientModel;
using System.ClientModel.Primitives;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text.Json;
using Betalgo.Ranul.OpenAI;
using Betalgo.Ranul.OpenAI.Managers;
using LLMClient.Endpoints;
using LLMClient.Endpoints.OpenAIAPI;
using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using OpenAI;
using OpenAI.Chat;
using OpenAI.Responses;

namespace LLMClient.Test;

public class APITest
{
    [Fact]
    [Experimental("SCME0001")]
    public async Task GetStreamingChatMessageContentsWorksCorrectlyAsync()
    {
        var httpMessageHandlerStub = new HttpMessageHandlerStub();
        var httpClient = new HttpClient(httpMessageHandlerStub);
        var fullPath = Path.GetFullPath("StreamingResponse.txt");
        await using (var fileStream = File.OpenRead(fullPath))
        {
            var httpResponseMessage = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StreamContent(fileStream)
            };
            httpMessageHandlerStub.ResponseToReturn = httpResponseMessage;
            /*IChatClient chatClient = new OpenAIService(new OpenAIOptions() { ApiKey = "asdf", DefaultModelId = "asdf", },
                httpClient){};*/
            
            
            
            var openAiClient = new OpenAIClientEx(new ApiKeyCredential("apiToken"), new OpenAIClientOptions()
            {
                Endpoint = new Uri("https://api.openai.net/v1/"),
                Transport = new HttpClientPipelineTransport(httpClient),
                RetryPolicy = new ClientRetryPolicy(0),
                NetworkTimeout = Timeout.InfiniteTimeSpan
            });
            var builder = Kernel.CreateBuilder();
            var kernel = builder.AddOpenAIChatCompletion("gpt-4o", openAiClient).Build();
            var chatClient = kernel.GetRequiredService<IChatCompletionService>().AsChatClient();
            using (chatClient)
            {
                var updates = await chatClient.GetStreamingResponseAsync([]).ToArrayAsync();
                foreach (var chatResponseUpdate in updates)
                {
                    var rawRepresentation = (StreamingChatCompletionUpdate)chatResponseUpdate.RawRepresentation;
                    using (var jsonDocument = JsonDocument.Parse(rawRepresentation.Patch.ToString("J")))
                    {
                        
                    }

                    Debugger.Break();
                }

                var mergeResponse = updates.MergeResponse();
                Debugger.Break();
            }
        }
    }
}