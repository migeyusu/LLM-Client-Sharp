using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
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
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace LLMClient.Test;

//dotnet publish .\LLMClient.csproj -p:PublishProfile=FolderProfile
public class UnitTest1
{
    private ITestOutputHelper output;

    IServiceProvider serviceProvider;

    public UnitTest1(ITestOutputHelper output)
    {
        this.output = output;
        serviceProvider = new ServiceCollection()
            .AddTransient<AutoMapModelTypeConverter>()
            .AddTransient<GlobalConfig>()
            .AddSingleton<IPromptsResource, PromptsResourceViewModel>()
            .AddSingleton<IEndpointService, EndpointConfigureViewModel>()
            .AddSingleton<IMcpServiceCollection, McpServiceCollection>()
            .AddSingleton<IBuiltInFunctionsCollection, BuiltInFunctionsCollection>()
            .AddMap().BuildServiceProvider();
    }

    [Fact]
    public async Task Convert()
    {
        // await Version3Converter.Convert("D:\\Dev\\LLM-Client-Sharp\\AIClient\\bin\\Debug\\net8.0-windows\\Dialogs");
    }

    [Fact]
    public void FunctionCallSerialize()
    {
        var functionCallContent = new FunctionCallContent("123", "test", new Dictionary<string, object?>
        {
            { "param1", "value1" },
            { "param2", 123 },
            { "param3", true }
        });
        var serializerOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            ReferenceHandler = ReferenceHandler.Preserve,
        };
        var serialize = JsonSerializer.Serialize(functionCallContent, serializerOptions);
        var callContent = JsonSerializer.Deserialize<FunctionCallContent>(serialize, serializerOptions);
        Assert.NotNull(callContent);
    }

    [Fact]
    public void FunctionCallGeneratorSerialize()
    {
        var projectPersistModel = new ProjectPersistModel
        {
            Tasks =
            [
                new ProjectTaskPersistModel()
                {
                    DialogItems = new IDialogPersistItem[]
                    {
                        new RequestViewItem()
                        {
                            TextMessage = "test",
                            FunctionGroups = new IAIFunctionGroup[]
                            {
                                new FileSystemPlugin()
                                {
                                    AllowedPaths = new ObservableCollection<string> { "C:\\", "D:\\" },
                                },
                                new WinCLIPlugin()
                            }
                        },
                        new MultiResponsePersistItem()
                        {
                            ResponseItems = new ResponsePersistItem[]
                            {
                                new ResponsePersistItem()
                                {
                                    ResponseMessages = new List<ChatMessagePO>()
                                    {
                                        new ChatMessagePO()
                                        {
                                            Role = ChatRole.Assistant,
                                            Contents = new List<IAIContent>()
                                            {
                                                new TextContentPO() { Text = "haode" },
                                                new FunctionCallContentPO()
                                                {
                                                    Name = "tool_0_FileSystem_WriteFile",
                                                    CallId = "FileSystem_WriteFile",
                                                    Arguments = new Dictionary<string, object?>()
                                                    {
                                                        { "filePath", "C:\\test.txt" },
                                                        { "content", "Hello World!" }
                                                    }
                                                }
                                            }
                                        },
                                        new ChatMessagePO()
                                        {
                                            Role = ChatRole.Tool,
                                            Contents = new List<IAIContent>()
                                            {
                                                new FunctionResultContentPO()
                                                {
                                                    CallId = "tool_0_FileSystem_WriteFile",
                                                    Result = "ok",
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            ]
        };
        var options = FileBasedSessionBase.SerializerOption;
        var serialize = JsonSerializer.Serialize(projectPersistModel, options);
        var callContent = JsonSerializer.Deserialize<FunctionCallContent>(serialize, options);
        Assert.NotNull(callContent);
    }

    [Fact]
    public void Mapping()
    {
        var mapper = serviceProvider.GetService<IMapper>();
        var dialogSession = new DialogFileViewModel(new DialogViewModel("sadg", new NullLlmModelClient()));
        var dialogSessionPersistModel =
            mapper?.Map<DialogFileViewModel, DialogFilePersistModel>(dialogSession, new DialogFilePersistModel());
        Assert.NotNull(dialogSessionPersistModel);
    }

    [Fact]
    public void CircularMapping()
    {
        var mapper = serviceProvider.GetService<IMapper>();
        var client = new TestLLMClient();
        var dialogViewModel = new DialogViewModel("test", client);
        var multiResponseViewItem = new MultiResponseViewItem(dialogViewModel);
        multiResponseViewItem.Append(new ResponseViewItem(client));
        multiResponseViewItem.Append(new ResponseViewItem(client));
        dialogViewModel.DialogItems.Add(multiResponseViewItem);
        var dialogFileViewModel = new DialogFileViewModel(dialogViewModel);
        var dialogFilePersistModel =
            mapper?.Map<DialogFileViewModel, DialogFilePersistModel>(dialogFileViewModel, (options => { }));
        Assert.NotNull(dialogFilePersistModel);
        var multiResponsePersistItem = dialogFilePersistModel.DialogItems?.FirstOrDefault() as MultiResponsePersistItem;
        Assert.NotNull(multiResponsePersistItem);
        var persistItems = multiResponsePersistItem.ResponseItems;
        Assert.Same(persistItems[0].Client, persistItems[1].Client);
    }

    [Fact]
    public void CircularSerialize()
    {
        var mapper = serviceProvider.GetService<IMapper>()!;
        var client = new TestLLMClient();
        var dialogViewModel = new DialogViewModel("test", client);
        var multiResponseViewItem = new MultiResponseViewItem(dialogViewModel);
        multiResponseViewItem.Append(new ResponseViewItem(client));
        multiResponseViewItem.Append(new ResponseViewItem(client));
        dialogViewModel.DialogItems.Add(multiResponseViewItem);
        var dialogFileViewModel = new DialogFileViewModel(dialogViewModel);
        var dialogFilePersistModel =
            mapper.Map<DialogFileViewModel, DialogFilePersistModel>(dialogFileViewModel, (options => { }));
        var serializerOptions = new JsonSerializerOptions()
        {
            WriteIndented = true, ReferenceHandler = ReferenceHandler.Preserve,
        };
        var serialize = JsonSerializer.Serialize(dialogFilePersistModel,
            serializerOptions);
        output.WriteLine(serialize);
        var filePersistModel = JsonSerializer.Deserialize<DialogFilePersistModel>(serialize, serializerOptions);
        var viewModel = mapper.Map<DialogFilePersistModel, DialogFileViewModel>(filePersistModel!, (options => { }));
        var multiResponseViewItemDe = viewModel.Dialog.DialogItems.First() as MultiResponseViewItem;
        var responseViewItems = multiResponseViewItemDe!.Items.OfType<ResponseViewItem>().ToArray();
        Assert.Same(responseViewItems[0].Client, responseViewItems[1].Client);
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