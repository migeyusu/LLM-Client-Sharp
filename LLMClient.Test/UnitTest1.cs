using System.ClientModel;
using System.ClientModel.Primitives;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
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
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.Data;
using Microsoft.SemanticKernel.Plugins.Web.Bing;
using Microsoft.SemanticKernel.Plugins.Web.Google;
using OpenAI;
using OpenAI.Responses;
using Xunit.Abstractions;
using FunctionCallContent = Microsoft.Extensions.AI.FunctionCallContent;

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
            .AddTransient<GlobalOptions>()
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
    public void ChatMapping()
    {
        var chatMessage = new ChatMessage(ChatRole.Assistant, "Hello World!");
        var mapper = serviceProvider.GetService<IMapper>();
        var chatMessagePo = mapper!.Map<ChatMessage, ChatMessagePO>(chatMessage);
        Assert.NotNull(chatMessagePo);
    }
    
    [Fact]
    public void ResponseDe()
    {
        var mockPipelineResponse = new MockPipelineResponse(200);
        var responseJson =
            "{\n  \"id\" : \"gen-1753352467-AoMxWIeYtcpVuwFugrRM\",\n  \"provider\" : \"Google\",\n  \"model\" : \"google/gemini-2.5-pro\",\n  \"object\" : \"chat.completion\",\n  \"created\" : 1753352470,\n  \"choices\" : [ {\n    \"logprobs\" : null,\n    \"finish_reason\" : \"stop\",\n    \"native_finish_reason\" : \"STOP\",\n    \"index\" : 0,\n    \"message\" : {\n      \"role\" : \"assistant\",\n      \"content\" : \"好的，我们来深入了解这款处理器的详细参数。\\n\\n首先需要澄清一点，您提到的“AI 9 Max 395+”在正式命名上可能有些出入。根据 AMD 在 Computex 2024 上发布的信息，该系列中最顶级的型号是 **AMD Ryzen™ AI 9 HX 370**。这很可能就是您想了解的处理器，它是新发布的 **Ryzen AI 300 系列**（代号 \\\"Strix Point\\\"）的旗舰产品。\\n\\n值得注意的是，您提供的搜索结果是关于较旧的 Intel Xeon 和 AMD Ryzen 5000 系列处理器的，其中并不包含这款最新 AI 300 系列处理器的详细信息 [amd.com](https://www.amd.com/en/products/processors/desktops/ryzen/5000-series/amd-ryzen-9-5950x.html)[intel.com](https://www.intel.com/content/www/us/en/products/sku/82765/intel-xeon-processor-e51650-v3-15m-cache-3-50-ghz/specifications.html)。因此，以下参数是基于发布会和各大科技媒体的权威报道整理的。\\n\\n### AMD Ryzen™ AI 9 HX 370 详细参数\\n\\n以下是这款旗舰处理器的核心规格：\\n\\n| 项目 | 详细参数 | 备注 |\\n| :--- | :--- | :--- |\\n| **处理器名称** | AMD Ryzen™ AI 9 HX 370 | 隶属于 Ryzen AI 300 系列 |\\n| **CPU 核心架构** | Zen 5 | 全新一代高性能 CPU 核心 |\\n| **CPU 核心/线程** | 12 核心 / 24 线程 | 采用混合核心设计 |\\n| **CPU 核心构成** | 4x Zen 5 + 8x Zen 5c | Zen 5c 是高密度、高能效核心，但功能与 Zen 5 核心相同 |\\n| **最高加速频率** | 高达 5.1 GHz | 指的是 Zen 5 高性能核心能达到的最高频率 |\\n| **总缓存** | 36 MB | (12 MB L2 缓存 + 24 MB L3 缓存) |\\n| **集成显卡 (iGPU)** | **AMD Radeon™ 890M** | 基于最新的 RDNA 3.5 架构，性能强大 |\\n| **iGPU 计算单元** | **16 CUs** (Compute Units) | 提供了非常强的集成图形处理能力，甚至可媲美部分入门级独显 |\\n| **神经处理单元 (NPU)** | **AMD XDNA™ 2** | AMD 第三代 AI 引擎，专为高效 AI 计算设计 |\\n| **NPU AI 算力** | **50 TOPS** | TOPS = 每秒万亿次运算，这是衡量 AI 性能的关键指标 |\\n| **热设计功耗 (TDP)** | 28W (可配置 15W-54W) | 笔记本厂商可根据模具散热能力进行调整 |\\n| **制造工艺** | 台积电 (TSMC) 4nm | 先进的制造工艺，有助于提升能效比 |\\n\\n### 关键亮点解析\\n\\n1.  **强大的 NPU (XDNA 2)**\\n    *   最引人注目的就是其高达 **50 TOPS** 的 NPU 算力。这远超微软为下一代“Copilot+ PC”设定的 40 TOPS 门槛。\\n    *   这意味着搭载这款处理器的笔记本电脑，可以在本地、离线状态下高效运行复杂的 AI 大语言模型和应用，如实时翻译、图像生成、代码辅助等，同时降低延迟和功耗。\\n\\n2.  **混合架构的 Zen 5 CPU**\\n    *   它首次在 AMD 移动处理器中采用了类似 Intel P-core/E-core 的混合设计，但 AMD 的方法是使用 4 个全尺寸的 Zen 5 核心和 8 个更高密度的 Zen 5c 核心。\\n    *   与 Intel 不同的是，AMD 的 Zen 5 和 Zen 5c 核心**都支持超线程 (SMT)**，因此总共能提供 24 个线程，这在多任务处理和重度生产力应用中优势明显。\\n\\n3.  **顶级的集成显卡 (Radeon 890M)**\\n    *   拥有 16 个 RDNA 3.5 计算单元的 Radeon 890M，是目前市面上性能最强的集成显卡之一。\\n    *   它的性能足以在 1080p 分辨率、中低画质下流畅运行许多现代 3A 游戏大作，让轻薄本也具备了不错的游戏能力。\\n\\n**总结来说，AMD Ryzen™ AI 9 HX 370 是一款专为 AI 时代打造的、性能极其全面的笔记本处理器。** 它不仅在传统的 CPU 和 GPU 性能上表现出色，更通过其领先的 NPU 算力，为下一代 AI PC 树立了新的标杆，是英特尔 Lunar Lake 和苹果 M 系列芯片的有力竞争者。预计搭载该处理器的笔记本电脑将在 2024 年下半年陆续上市。\",\n      \"refusal\" : null,\n      \"reasoning\" : \"**Understanding the User's Needs**\\n\\nI'm focusing on unpacking the user's request. They want detailed parameters for the AMD Ryzen AI 9 HX 370. I see the previous response did well to clarify what the user meant, but I need to consider other possible nuances and ensure I don't miss anything important in my response. This is about precision.\\n\\n\\n**Formulating a Targeted Response**\\n\\nOkay, I'm refining my approach. I've identified the likely user intent and the naming discrepancy, aiming to provide the correct specs. I'll start with the searches to pinpoint the exact details. My plan is to address the query promptly, while being very clear in identifying the parameters. Then I'll synthesize and present the information in an accessible way. I am preparing the user's expectations.\\n\\n\\n**Detailing Search Queries**\\n\\nI'm now fine-tuning the search strings. Considering the user's likely intent, I'm optimizing the queries to capture the most crucial information. This involves testing different permutations, like combining specs with \\\"Zen 5c\\\" and specifying the NPU and GPU details. I'll prioritize official AMD sources and reputable tech sites to ensure accuracy in identifying and collecting data points.\\n\\n\\n**Gathering the Details**\\n\\nI'm now fully immersed in extracting the salient specifications. I've analyzed the simulated search results, organizing the data into a clear structure. The focus is on creating a concise \\\"Key Specifications\\\" table, including core counts, clock speeds, and architecture details for ease of understanding. I'll also be highlighting key information, like the NPU's TOPS, and explaining the implications of the hybrid core design.\\n\\n\\n**Organizing the Data**\\n\\nI'm focusing on distilling the key details into a structured response. I have the specifications from the simulated search, and I'm now working on the \\\"Key Specifications\\\" table. It will have processor name, CPU and GPU details, and NPU performance, as I continue to think about the best way to make the information easily digestible and understandable. The explanation of hybrid cores and the significance of the NPU are also top of mind.\\n\\n\\n**Synthesizing the Response**\\n\\nI'm now formulating the comprehensive response. I will begin by acknowledging the user's likely intent and clarifying the processor's name. The \\\"Key Specifications\\\" table is coming together, incorporating CPU, GPU, and NPU details. I am also drafting explanatory paragraphs to accompany the table, focusing on hybrid cores, NPU performance, and the implications for AI and overall system performance.\\n\\n\\n**Constructing the Final Answer**\\n\\nI'm now formulating the full response, incorporating the clarified naming and a detailed \\\"Key Specifications\\\" table, derived from the hypothetical search. This includes core configurations and the NPU's impressive capabilities. I am providing the user with a very accurate and useful set of information. After this, I'll summarize the key points and provide a conclusion, including a timeframe for the product launch.\\n\\n\\n**Refining the Structure**\\n\\nI'm now focusing on structuring the response. I will correct the naming error and present the detailed specifications in a well-organized format, specifically a \\\"Key Specifications\\\" table. Crucially, I plan to explain the hybrid core design and the role of the NPU. The ultimate goal is to provide clear and actionable information.\\n\\n\\n\",\n      \"reasoning_details\" : [ {\n        \"type\" : \"reasoning.text\",\n        \"text\" : \"**Understanding the User's Needs**\\n\\nI'm focusing on unpacking the user's request. They want detailed parameters for the AMD Ryzen AI 9 HX 370. I see the previous response did well to clarify what the user meant, but I need to consider other possible nuances and ensure I don't miss anything important in my response. This is about precision.\\n\\n\\n**Formulating a Targeted Response**\\n\\nOkay, I'm refining my approach. I've identified the likely user intent and the naming discrepancy, aiming to provide the correct specs. I'll start with the searches to pinpoint the exact details. My plan is to address the query promptly, while being very clear in identifying the parameters. Then I'll synthesize and present the information in an accessible way. I am preparing the user's expectations.\\n\\n\\n**Detailing Search Queries**\\n\\nI'm now fine-tuning the search strings. Considering the user's likely intent, I'm optimizing the queries to capture the most crucial information. This involves testing different permutations, like combining specs with \\\"Zen 5c\\\" and specifying the NPU and GPU details. I'll prioritize official AMD sources and reputable tech sites to ensure accuracy in identifying and collecting data points.\\n\\n\\n**Gathering the Details**\\n\\nI'm now fully immersed in extracting the salient specifications. I've analyzed the simulated search results, organizing the data into a clear structure. The focus is on creating a concise \\\"Key Specifications\\\" table, including core counts, clock speeds, and architecture details for ease of understanding. I'll also be highlighting key information, like the NPU's TOPS, and explaining the implications of the hybrid core design.\\n\\n\\n**Organizing the Data**\\n\\nI'm focusing on distilling the key details into a structured response. I have the specifications from the simulated search, and I'm now working on the \\\"Key Specifications\\\" table. It will have processor name, CPU and GPU details, and NPU performance, as I continue to think about the best way to make the information easily digestible and understandable. The explanation of hybrid cores and the significance of the NPU are also top of mind.\\n\\n\\n**Synthesizing the Response**\\n\\nI'm now formulating the comprehensive response. I will begin by acknowledging the user's likely intent and clarifying the processor's name. The \\\"Key Specifications\\\" table is coming together, incorporating CPU, GPU, and NPU details. I am also drafting explanatory paragraphs to accompany the table, focusing on hybrid cores, NPU performance, and the implications for AI and overall system performance.\\n\\n\\n**Constructing the Final Answer**\\n\\nI'm now formulating the full response, incorporating the clarified naming and a detailed \\\"Key Specifications\\\" table, derived from the hypothetical search. This includes core configurations and the NPU's impressive capabilities. I am providing the user with a very accurate and useful set of information. After this, I'll summarize the key points and provide a conclusion, including a timeframe for the product launch.\\n\\n\\n**Refining the Structure**\\n\\nI'm now focusing on structuring the response. I will correct the naming error and present the detailed specifications in a well-organized format, specifically a \\\"Key Specifications\\\" table. Crucially, I plan to explain the hybrid core design and the role of the NPU. The ultimate goal is to provide clear and actionable information.\\n\\n\\n\",\n        \"format\" : \"unknown\"\n      } ],\n      \"annotations\" : [ {\n        \"type\" : \"url_citation\",\n        \"url_citation\" : {\n          \"end_index\" : 0,\n          \"start_index\" : 0,\n          \"title\" : \"Intel® Xeon® Processor E5-1650 v3 (15M Cache, 3.50 GHz) - Product Specifications | Intel\",\n          \"url\" : \"https://www.intel.com/content/www/us/en/products/sku/82765/intel-xeon-processor-e51650-v3-15m-cache-3-50-ghz/specifications.html\",\n          \"content\" : \"- [Specifications](https://www.intel.com/content/www/us/en/products/sku/82765/intel-xeon-processor-e51650-v3-15m-cache-3-50-ghz/specifications.html) - [Ordering & Compliance](https://www.intel.com/content/www/us/en/products/sku/82765/intel-xeon-processor-e51650-v3-15m-cache-3-50-ghz/ordering.html) - [Support](https://www.intel.com/content/www/us/en/products/sku/82765/intel-xeon-processor-e51650-v3-15m-cache-3-50-ghz/support.html) All information provided is subject to change at any time, without notice. Intel may make changes to manufacturing life cycle, specifications, and product descriptions at any time, without notice.\"\n        }\n      }, {\n        \"type\" : \"url_citation\",\n        \"url_citation\" : {\n          \"end_index\" : 0,\n          \"start_index\" : 0,\n          \"title\" : \"Intel® Xeon® Processor E5-1600/2400/2600/4600 (E5-Product Family) Product Families Datasheet- Volume Two\",\n          \"url\" : \"https://www.intel.com/content/dam/www/public/us/en/documents/datasheets/xeon-e5-1600-2600-vol-2-datasheet.pdf\",\n          \"content\" : \"2 Intel® Xeon® Processor E5-1600/2400/2600/4600 (E5-Product Family) Product Families Legal Lines and Disclaimers INFORMATION IN THIS DOCUMENT IS PROVIDED IN CONNECTION WITH INTEL® PRODUCTS. NO LICENSE, EXPRESS OR IMPLIED,  BY ESTOPPEL OR OTHERWISE, TO ANY INTELLECTUAL PROPERTY RIGHTS IS GRANTED BY THIS DOCUMENT. EXCEPT AS \"\n        }\n      }, {\n        \"type\" : \"url_citation\",\n        \"url_citation\" : {\n          \"end_index\" : 0,\n          \"start_index\" : 0,\n          \"title\" : \"英特尔® 产品规范\",\n          \"url\" : \"https://www.intel.cn/content/www/cn/zh/ark.html\",\n          \"content\" : \" 您可以使用几种方式轻松搜索整个 Intel.com 网站。  “Ice Lake”、Ice AND Lake、Ice OR Lake、Ice*  您也可以尝试使用以下快速链接查看最受欢迎搜索的结果。   2100 系列 IPU 适配器（最高支持 200GbE） \"\n        }\n      }, {\n        \"type\" : \"url_citation\",\n        \"url_citation\" : {\n          \"end_index\" : 0,\n          \"start_index\" : 0,\n          \"title\" : \"AMD Ryzen™ 9 5950X Desktop Processor\",\n          \"url\" : \"https://www.amd.com/en/products/processors/desktops/ryzen/5000-series/amd-ryzen-9-5950x.html\",\n          \"content\" : \"[AMD Website Accessibility Statement](https://www.amd.com/en/site-notifications/accessibility-statement.html)\\n\\n- Image Zoom\\n\\n\\nAMD Ryzen™ 9 5950X. One processor that can game as well as it creates. - [Drivers & Support](https://www.amd.com/en/support/downloads/drivers.html/processors/ryzen/ryzen-5000-series/amd-ryzen-9-5950x.html)\\n\\n\\n### 16 Cores. 0 Compromises. ### Drivers and Resources\\n\\n### [Driver & Software Downloads](https://www.amd.com/en/support/download/drivers.html)\\n\\nAccess the latest drivers, software and release notes for AMD products.\"\n        }\n      }, {\n        \"type\" : \"url_citation\",\n        \"url_citation\" : {\n          \"end_index\" : 0,\n          \"start_index\" : 0,\n          \"title\" : \"Datasheet Vol. 2: Intel® Core™ i7 Processor Family for LGA2011-v3 Socket\",\n          \"url\" : \"https://www.intel.cn/content/dam/www/public/us/en/documents/datasheets/core-i7-6xxx-lga2011-v3-datasheet-vol-2.pdf\",\n          \"content\" : \"Supporting Desktop Intel® Core™ i7-6950X Extreme Edition Processor for  Supporting Desktop Intel® Core™ i7-6900K, i7-6850K, and i7-6800K  Legal Lines and Disclaimers UNLESS OTHERWISE AGREED IN WRITING BY INTEL, THE INTEL PRODUCTS ARE NOT DESIGNED NOR INTENDED FOR ANY APPLICATION IN WHICH THE FAILURE OF THE INTEL PRODUCT COULD CREATE A SITUATION WHERE PERSONAL INJURY OR DEATH MAY OCCUR. You may not use or facilitate the use of this document in connection with any infringement or other legal analysis concerning Intel  products described herein.\"\n        }\n      } ]\n    }\n  } ],\n  \"usage\" : {\n    \"prompt_tokens\" : 2298,\n    \"completion_tokens\" : 3099,\n    \"total_tokens\" : 5397\n  }\n}";
        mockPipelineResponse.SetContent(responseJson);
        OpenAIResponse response = (OpenAIResponse)ClientResult.FromResponse(mockPipelineResponse);
        output.WriteLine("Response ID: " + response.Id);
    }


    [Fact]
    [Experimental("SKEXP0050")]
    public async Task Search()
    {
        // Create an ITextSearch instance using Google search
        var textSearch = new GoogleTextSearch(
            initializer: new()
                { ApiKey = "" }, "");
        var query = "What is the Semantic Kernel?";

// Search and return results as string items
        KernelSearchResults<string> stringResults = await textSearch.SearchAsync(query, new() { Top = 4, Skip = 0 });
        output.WriteLine("——— String Results ———\n");
        await foreach (string result in stringResults.Results)
        {
            output.WriteLine(result);
        }

// Search and return results as TextSearchResult items
        KernelSearchResults<TextSearchResult> textResults =
            await textSearch.GetTextSearchResultsAsync(query, new() { Top = 4, Skip = 4 });
        output.WriteLine("\n——— Text Search Results ———\n");
        await foreach (TextSearchResult result in textResults.Results)
        {
            output.WriteLine($"Name:  {result.Name}");
            output.WriteLine($"Value: {result.Value}");
            output.WriteLine($"Link:  {result.Link}");
        }

// Search and return results as Google.Apis.CustomSearchAPI.v1.Data.Result items
        KernelSearchResults<object> fullResults =
            await textSearch.GetSearchResultsAsync(query, new() { Top = 4, Skip = 8 });
        output.WriteLine("\n——— Google Web Page Results ———\n");
        await foreach (Google.Apis.CustomSearchAPI.v1.Data.Result result in fullResults.Results)
        {
            output.WriteLine($"Title:       {result.Title}");
            output.WriteLine($"Snippet:     {result.Snippet}");
            output.WriteLine($"Link:        {result.Link}");
            output.WriteLine($"DisplayLink: {result.DisplayLink}");
            output.WriteLine($"Kind:        {result.Kind}");
        }
    }

    [Fact]
    [Experimental("SKEXP0050")]
    public void GetSearchTools()
    {
        var textSearch = new GoogleTextSearch(
            initializer: new()
                { ApiKey = "" }, "");
        var searchPlugin = textSearch.CreateWithGetTextSearchResults("GoogleTextSearch");
        output.WriteLine("Plugin Name: " + searchPlugin.Name);
        output.WriteLine("Plugin Description: " + searchPlugin.Description);
        output.WriteLine("Plugin Functions: " + searchPlugin.FunctionCount);
        foreach (var function in searchPlugin)
        {
            output.WriteLine("Function Name: " + function.Name);
            output.WriteLine("Function Description: " + function.Description);
            foreach (var parameter in function.Metadata.Parameters)
            {
                output.WriteLine($"  Parameter: {parameter.Name}");
                output.WriteLine($"    Description: {parameter.Description}");
                output.WriteLine($"    Type: {parameter.ParameterType}");
                output.WriteLine($"    Is Required: {parameter.IsRequired}");
                output.WriteLine($"    Default Value: {parameter.DefaultValue}");
            }
        }
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
                            FunctionGroups = new List<IAIFunctionGroup>()
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
        var callContent = JsonSerializer.Deserialize<ProjectPersistModel>(serialize, options);
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
        var client = new FakeLLMClient();
        var dialogViewModel = new DialogViewModel("test", client);
        var multiResponseViewItem = new MultiResponseViewItem(dialogViewModel);
        multiResponseViewItem.Append(new ResponseViewItem(client));
        multiResponseViewItem.Append(new ResponseViewItem(client));
        dialogViewModel.DialogItems.Add(multiResponseViewItem);
        var dialogFileViewModel = new DialogFileViewModel(dialogViewModel);
        var dialogFilePersistModel =
            mapper?.Map<DialogFileViewModel, DialogFilePersistModel>(dialogFileViewModel, (options => { }));
        Assert.NotNull(dialogFilePersistModel);
        var multiResponsePersistItem =
            dialogFilePersistModel.DialogItems?.FirstOrDefault() as MultiResponsePersistItem;
        Assert.NotNull(multiResponsePersistItem);
        var persistItems = multiResponsePersistItem.ResponseItems;
        Assert.Same(persistItems[0].Client, persistItems[1].Client);
    }

    [Fact]
    public void CircularSerialize()
    {
        var mapper = serviceProvider.GetService<IMapper>()!;
        var client = new FakeLLMClient();
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
        var viewModel =
            mapper.Map<DialogFilePersistModel, DialogFileViewModel>(filePersistModel!, (options => { }));
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
            using (var httpRequestMessage =
                   new HttpRequestMessage(HttpMethod.Get, "https://xiaoai.plus/api/pricing"))
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
    public void TestNode()
    {
                
        var foo = new[]
        {
            new
            {
                id = "web",
            }
        };
        var node = JsonSerializer.SerializeToNode(foo);
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

    [Fact]
    public void APIOption()
    {
        var promptExecutionSettings = new PromptExecutionSettings()
        {
            ExtensionData = new Dictionary<string, object>()
            {
                {
                    "id", "web"
                }
            }
        };
        var openAiPromptExecutionSettings = OpenAIPromptExecutionSettings.FromExecutionSettings(promptExecutionSettings);
        
    }
}