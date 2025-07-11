using System.Diagnostics;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using AutoMapper;
using LLMClient.Abstraction;
using LLMClient.Data;
using LLMClient.Endpoints;
using LLMClient.Endpoints.OpenAIAPI;
using LLMClient.Obsolete;
using LLMClient.UI;
using LLMClient.UI.Dialog;
using LLMClient.UI.MCP;
using LLMClient.UI.MCP.Servers;
using LLMClient.UI.Project;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace LLMClient.Test;

//dotnet publish .\LLMClient.csproj -p:PublishProfile=FolderProfile
public class UnitTest1
{
    private ITestOutputHelper output;

    public UnitTest1(ITestOutputHelper output)
    {
        this.output = output;
    }

    [Fact]
    public async Task Convert()
    {
        // await Version3Converter.Convert("D:\\Dev\\LLM-Client-Sharp\\AIClient\\bin\\Debug\\net8.0-windows\\Dialogs");
    }

    [Fact]
    public void Mapping()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton<IEndpointService, EndpointConfigureViewModel>()
            .AddSingleton<IMcpServiceCollection, McpServiceCollection>();
        serviceCollection.AddAutoMapper((provider, expression) =>
        {
            expression.CreateMap<DialogSessionPersistModel, DialogSession>()
                .ConvertUsing<AutoMapModelTypeConverter>();
            expression.CreateMap<DialogSession, DialogSessionPersistModel>()
                .ConvertUsing<AutoMapModelTypeConverter>();
            expression.CreateMap<DialogViewModel, DialogPersistModel>()
                .ConvertUsing<AutoMapModelTypeConverter>();
            expression.CreateMap<DialogPersistModel, DialogViewModel>()
                .ConvertUsing<AutoMapModelTypeConverter>();
            expression.CreateMap<APIEndPoint, APIEndPoint>();
            expression.CreateMap<APIDefaultOption, APIDefaultOption>();
            expression.CreateMap<ResponseViewItem, ResponsePersistItem>();
            expression.CreateMap<ResponsePersistItem, ResponseViewItem>().ConvertUsing<AutoMapModelTypeConverter>();
            expression.CreateMap<MultiResponsePersistItem, MultiResponseViewItem>()
                .ConvertUsing<AutoMapModelTypeConverter>();
            expression.CreateMap<MultiResponseViewItem, MultiResponsePersistItem>()
                .ConvertUsing<AutoMapModelTypeConverter>();
            expression.CreateMap<ProjectViewModel, ProjectPersistModel>()
                .ConvertUsing<AutoMapModelTypeConverter>();
            expression.CreateMap<ProjectPersistModel, ProjectViewModel>()
                .ConvertUsing<AutoMapModelTypeConverter>();
            expression.CreateMap<ProjectTask, ProjectTaskPersistModel>()
                .ConvertUsing<AutoMapModelTypeConverter>();
            expression.CreateMap<ProjectTaskPersistModel, ProjectTask>()
                .ConvertUsing<AutoMapModelTypeConverter>();
            // expression.CreateMap<AzureOption, GithubCopilotEndPoint>();
            expression.ConstructServicesUsing(provider.GetService);
        }, AppDomain.CurrentDomain.GetAssemblies());
        var serviceProvider = serviceCollection.BuildServiceProvider();
        var mapper = serviceProvider.GetService<IMapper>();
        var dialogSession = new DialogSession(new DialogViewModel("sadg", new NullLlmModelClient()));
        var dialogSessionPersistModel = mapper?.Map<DialogSession, DialogSessionPersistModel>(dialogSession,new DialogSessionPersistModel());
        Assert.NotNull(dialogSessionPersistModel);
    }

    [Fact]
    public async Task GetGithubModels()
    {
        HttpClientHandler handler = new HttpClientHandler()
            { AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate };
        using (var httpClient = new HttpClient(handler))
        {
            using (var httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, "https://xiaoai.plus/api/pricing"))
            {
                httpRequestMessage.Headers.Add("Referrer-Policy", "strict-origin-when-cross-origin");
                using (var message = await httpClient.SendAsync(httpRequestMessage))
                {
                    message.EnsureSuccessStatusCode();
                    var content = await message.Content.ReadAsStringAsync();
                    output.WriteLine(content);
                }
            }
        }
    }

    [Fact]
    public async Task UrlCheck()
    {
        using (var httpClient = new HttpClient())
        {
            using (var message = await httpClient.GetAsync("https://data.ocoolai.com/items/models?limit=1000"))
            {
                message.EnsureSuccessStatusCode();
                var readAsStringAsync = await message.Content.ReadAsStringAsync();
                output.WriteLine(readAsStringAsync);
            }
        }
    }

    [Fact]
    public async Task TestImageReqeust()
    {
        var url =
            "https://t0.gstatic.com/faviconV2?client=SOCIAL&type=FAVICON&fallback_opts=TYPE,SIZE,URL&url=https://chutes.ai/&size=256";
        var extension = Path.GetExtension(url);
        using (var cancellationTokenSource = new CancellationTokenSource(5000))
        {
            var cancellationToken = cancellationTokenSource.Token;
            using (var message = await new HttpClient().GetAsync(url, cancellationToken))
            {
                message.EnsureSuccessStatusCode();
                if (string.IsNullOrEmpty(extension))
                {
                    var mediaType = message.Content.Headers.ContentType?.MediaType;
                    if (!string.IsNullOrEmpty(mediaType))
                    {
                        Debugger.Break();
                    }
                }

                if (string.IsNullOrEmpty(extension))
                {
                    Debugger.Break();
                }
            }
        }
    }
}