using System.ClientModel;
using System.ClientModel.Primitives;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows.Media.Imaging;
using ImageMagick;
using LLMClient.Abstraction;
using LLMClient.Component.ViewModel;
using LLMClient.Configuration;
using LLMClient.Data;
using LLMClient.Dialog;
using LLMClient.Dialog.Models;
using LLMClient.Endpoints;
using LLMClient.Endpoints.Azure.Models;
using LLMClient.Endpoints.Messages;
using LLMClient.Endpoints.OpenAIAPI;
using LLMClient.Rag;
using LLMClient.ToolCall;
using LLMClient.ToolCall.DefaultPlugins;
using LLMClient.ToolCall.MCP;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel.Connectors.SqliteVec;
using Microsoft.SemanticKernel.Data;
using OpenAI;
using OpenAI.Chat;
using Xunit.Abstractions;
using ChatMessage = OpenAI.Chat.ChatMessage;

namespace LLMClient.Test;

public class UnitTest2
{
    private ITestOutputHelper output;

    IServiceProvider serviceProvider;

    public UnitTest2(ITestOutputHelper output)
    {
        this.output = output;
        serviceProvider = new ServiceCollection()
            .AddTransient<AutoMapModelTypeConverter>()
            .AddTransient<GlobalOptions>()
            .AddSingleton<IPromptsResource, PromptsResourceViewModel>()
            .AddSingleton<IEndpointService, EndpointConfigureViewModel>()
            .AddSingleton<IMcpServiceCollection, McpServiceCollection>()
            .AddSingleton<BuiltInFunctionsCollection>()
            .AddMap().BuildServiceProvider();
    }

    [Fact]
    public async Task AzureDetailAsync()
    {
        using (var httpClient = new HttpClient())
        {
            using (var responseMessage = await httpClient.GetAsync("https://github.com/models/available"))
            {
                using (var httpContent = responseMessage.Content)
                {
                    var message = await httpContent.ReadAsStringAsync();
                    var jsonNode = JsonNode.Parse(message);
                    var node = jsonNode!["payload"];
                    var modelInfo = node.Deserialize<AzureDetailModelInfo>()!;
                    output.WriteLine(modelInfo.ToString());
                }
            }
        }
    }

    [Fact]
    public async Task Convert()
    {
        /*// 1. 假设这是您从某个地方获取的原始 BinaryContent 对象
        // 它包含一个简单的 JSON 对象
        var originalJson = """
                           {
                               "someProperty": "someValue",
                               "anotherProperty": 123
                           }
                           """;
        var bytes = Encoding.UTF8.GetBytes(originalJson);
        await using (var memoryStream = new MemoryStream(bytes))
        {
            BinaryContent originalContent = BinaryContent.Create(memoryStream);

            output.WriteLine("原始 BinaryContent 内容:");
            output.WriteLine(originalContent.ToString());
            output.WriteLine(new string('-', 20));

            // 2. 读取内容、解析并添加新属性
            // 将 BinaryContent 转换为字符串
            var requestString = originalContent.ToString();


            // 解析为 JsonNode
            var jsonNode = JsonNode.Parse(requestString);
            if (jsonNode == null)
            {
                output.WriteLine("无法将内容解析为 JSON。");
                return;
            }

            // 准备要添加的新节点
            var pluginsToAdd = new[]
            {
                new
                {
                    id = "web",
                    version = "1.0.0"
                }
            };
            var pluginsNode = JsonSerializer.SerializeToNode(pluginsToAdd);

            // 将新节点添加到 "plugins" 属性
            jsonNode["plugins"] = pluginsNode;
            jsonNode["processedTimestamp"] = DateTime.UtcNow; // 再加一个属性

            // 3. 将修改后的 JsonNode 序列化，并创建新的 BinaryContent
            // 注意：这里我们使用 JsonSerializer.Serialize() 来获得最终的字符串
            var modifiedJsonString =
                JsonSerializer.Serialize(jsonNode, new JsonSerializerOptions { WriteIndented = true });

            // 使用 BinaryContent.Create() 创建新实例
            // 它会接收一个 ReadOnlyMemory<byte>
            await using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(modifiedJsonString)))
            {
                BinaryContent modifiedContent = BinaryContent.Create(stream);
                output.WriteLine("修改后的 BinaryContent 内容:");
                output.WriteLine(modifiedContent.ToString());
            }
        }*/

        var originalJson = """
                           {
                               "someProperty": "someValue",
                               "anotherProperty": 123
                           }
                           """;
        var bytes = Encoding.UTF8.GetBytes(originalJson);
        BinaryContent binaryContent;
        await using (var oriStream = new MemoryStream(bytes))
        {
            binaryContent = BinaryContent.Create(oriStream);

            await using var memoryStream = new MemoryStream();
            await binaryContent
                .WriteToAsync(memoryStream); // Write content to stream (synchronous, but wrapped in async context)
            memoryStream.Position = 0; // Reset stream position

            // Step 2: Read as string (assume UTF-8 JSON)
            // string jsonString = await new StreamReader(memoryStream, Encoding.UTF8).ReadToEndAsync();

            // Step 3: Parse as JsonNode and modify
            JsonNode? jsonNode = await JsonNode.ParseAsync(memoryStream);
            if (jsonNode == null)
            {
                throw new InvalidOperationException("Content is not valid JSON.");
            }

            var foo = new[]
            {
                new { id = "web" }
            };
            JsonNode? node = JsonSerializer.SerializeToNode(foo);
            jsonNode["plugins"] = node;

            // Step 4: Serialize back to string and create new BinaryContent
            string newJson = jsonNode.ToJsonString();
            BinaryData newData = BinaryData.FromString(newJson);
            var content = BinaryContent.Create(newData);
        }
    }

    [Fact]
    public async Task Annotation()
    {
        var rawAudioResponse = BinaryData.FromBytes("""
                                                    {
                                                      "id" : "gen-1753352467-AoMxWIeYtcpVuwFugrRM",
                                                      "provider" : "Google",
                                                      "model" : "google/gemini-2.5-pro",
                                                      "object" : "chat.completion",
                                                      "created" : 1753352470,
                                                      "choices" : [ {
                                                        "logprobs" : null,
                                                        "finish_reason" : "stop",
                                                        "native_finish_reason" : "STOP",
                                                        "index" : 0,
                                                        "message" : {
                                                          "role" : "assistant",
                                                          "content" : "好的，我们来深入了解这款处理器的详细参数。\n\n首先需要澄清一点，您提到的“AI 9 Max 395+”在正式命名上可能有些出入。根据 AMD 在 Computex 2024 上发布的信息，该系列中最顶级的型号是 **AMD Ryzen™ AI 9 HX 370**。这很可能就是您想了解的处理器，它是新发布的 **Ryzen AI 300 系列**（代号 \"Strix Point\"）的旗舰产品。\n\n值得注意的是，您提供的搜索结果是关于较旧的 Intel Xeon 和 AMD Ryzen 5000 系列处理器的，其中并不包含这款最新 AI 300 系列处理器的详细信息 [amd.com](https://www.amd.com/en/products/processors/desktops/ryzen/5000-series/amd-ryzen-9-5950x.html)[intel.com](https://www.intel.com/content/www/us/en/products/sku/82765/intel-xeon-processor-e51650-v3-15m-cache-3-50-ghz/specifications.html)。因此，以下参数是基于发布会和各大科技媒体的权威报道整理的。\n\n### AMD Ryzen™ AI 9 HX 370 详细参数\n\n以下是这款旗舰处理器的核心规格：\n\n| 项目 | 详细参数 | 备注 |\n| :--- | :--- | :--- |\n| **处理器名称** | AMD Ryzen™ AI 9 HX 370 | 隶属于 Ryzen AI 300 系列 |\n| **CPU 核心架构** | Zen 5 | 全新一代高性能 CPU 核心 |\n| **CPU 核心/线程** | 12 核心 / 24 线程 | 采用混合核心设计 |\n| **CPU 核心构成** | 4x Zen 5 + 8x Zen 5c | Zen 5c 是高密度、高能效核心，但功能与 Zen 5 核心相同 |\n| **最高加速频率** | 高达 5.1 GHz | 指的是 Zen 5 高性能核心能达到的最高频率 |\n| **总缓存** | 36 MB | (12 MB L2 缓存 + 24 MB L3 缓存) |\n| **集成显卡 (iGPU)** | **AMD Radeon™ 890M** | 基于最新的 RDNA 3.5 架构，性能强大 |\n| **iGPU 计算单元** | **16 CUs** (Compute Units) | 提供了非常强的集成图形处理能力，甚至可媲美部分入门级独显 |\n| **神经处理单元 (NPU)** | **AMD XDNA™ 2** | AMD 第三代 AI 引擎，专为高效 AI 计算设计 |\n| **NPU AI 算力** | **50 TOPS** | TOPS = 每秒万亿次运算，这是衡量 AI 性能的关键指标 |\n| **热设计功耗 (TDP)** | 28W (可配置 15W-54W) | 笔记本厂商可根据模具散热能力进行调整 |\n| **制造工艺** | 台积电 (TSMC) 4nm | 先进的制造工艺，有助于提升能效比 |\n\n### 关键亮点解析\n\n1.  **强大的 NPU (XDNA 2)**\n    *   最引人注目的就是其高达 **50 TOPS** 的 NPU 算力。这远超微软为下一代“Copilot+ PC”设定的 40 TOPS 门槛。\n    *   这意味着搭载这款处理器的笔记本电脑，可以在本地、离线状态下高效运行复杂的 AI 大语言模型和应用，如实时翻译、图像生成、代码辅助等，同时降低延迟和功耗。\n\n2.  **混合架构的 Zen 5 CPU**\n    *   它首次在 AMD 移动处理器中采用了类似 Intel P-core/E-core 的混合设计，但 AMD 的方法是使用 4 个全尺寸的 Zen 5 核心和 8 个更高密度的 Zen 5c 核心。\n    *   与 Intel 不同的是，AMD 的 Zen 5 和 Zen 5c 核心**都支持超线程 (SMT)**，因此总共能提供 24 个线程，这在多任务处理和重度生产力应用中优势明显。\n\n3.  **顶级的集成显卡 (Radeon 890M)**\n    *   拥有 16 个 RDNA 3.5 计算单元的 Radeon 890M，是目前市面上性能最强的集成显卡之一。\n    *   它的性能足以在 1080p 分辨率、中低画质下流畅运行许多现代 3A 游戏大作，让轻薄本也具备了不错的游戏能力。\n\n**总结来说，AMD Ryzen™ AI 9 HX 370 是一款专为 AI 时代打造的、性能极其全面的笔记本处理器。** 它不仅在传统的 CPU 和 GPU 性能上表现出色，更通过其领先的 NPU 算力，为下一代 AI PC 树立了新的标杆，是英特尔 Lunar Lake 和苹果 M 系列芯片的有力竞争者。预计搭载该处理器的笔记本电脑将在 2024 年下半年陆续上市。",
                                                          "refusal" : null,
                                                          "reasoning" : "**Understanding the User's Needs**\n\nI'm focusing on unpacking the user's request. They want detailed parameters for the AMD Ryzen AI 9 HX 370. I see the previous response did well to clarify what the user meant, but I need to consider other possible nuances and ensure I don't miss anything important in my response. This is about precision.\n\n\n**Formulating a Targeted Response**\n\nOkay, I'm refining my approach. I've identified the likely user intent and the naming discrepancy, aiming to provide the correct specs. I'll start with the searches to pinpoint the exact details. My plan is to address the query promptly, while being very clear in identifying the parameters. Then I'll synthesize and present the information in an accessible way. I am preparing the user's expectations.\n\n\n**Detailing Search Queries**\n\nI'm now fine-tuning the search strings. Considering the user's likely intent, I'm optimizing the queries to capture the most crucial information. This involves testing different permutations, like combining specs with \"Zen 5c\" and specifying the NPU and GPU details. I'll prioritize official AMD sources and reputable tech sites to ensure accuracy in identifying and collecting data points.\n\n\n**Gathering the Details**\n\nI'm now fully immersed in extracting the salient specifications. I've analyzed the simulated search results, organizing the data into a clear structure. The focus is on creating a concise \"Key Specifications\" table, including core counts, clock speeds, and architecture details for ease of understanding. I'll also be highlighting key information, like the NPU's TOPS, and explaining the implications of the hybrid core design.\n\n\n**Organizing the Data**\n\nI'm focusing on distilling the key details into a structured response. I have the specifications from the simulated search, and I'm now working on the \"Key Specifications\" table. It will have processor name, CPU and GPU details, and NPU performance, as I continue to think about the best way to make the information easily digestible and understandable. The explanation of hybrid cores and the significance of the NPU are also top of mind.\n\n\n**Synthesizing the Response**\n\nI'm now formulating the comprehensive response. I will begin by acknowledging the user's likely intent and clarifying the processor's name. The \"Key Specifications\" table is coming together, incorporating CPU, GPU, and NPU details. I am also drafting explanatory paragraphs to accompany the table, focusing on hybrid cores, NPU performance, and the implications for AI and overall system performance.\n\n\n**Constructing the Final Answer**\n\nI'm now formulating the full response, incorporating the clarified naming and a detailed \"Key Specifications\" table, derived from the hypothetical search. This includes core configurations and the NPU's impressive capabilities. I am providing the user with a very accurate and useful set of information. After this, I'll summarize the key points and provide a conclusion, including a timeframe for the product launch.\n\n\n**Refining the Structure**\n\nI'm now focusing on structuring the response. I will correct the naming error and present the detailed specifications in a well-organized format, specifically a \"Key Specifications\" table. Crucially, I plan to explain the hybrid core design and the role of the NPU. The ultimate goal is to provide clear and actionable information.\n\n\n",
                                                          "reasoning_details" : [ {
                                                            "type" : "reasoning.text",
                                                            "text" : "**Understanding the User's Needs**\n\nI'm focusing on unpacking the user's request. They want detailed parameters for the AMD Ryzen AI 9 HX 370. I see the previous response did well to clarify what the user meant, but I need to consider other possible nuances and ensure I don't miss anything important in my response. This is about precision.\n\n\n**Formulating a Targeted Response**\n\nOkay, I'm refining my approach. I've identified the likely user intent and the naming discrepancy, aiming to provide the correct specs. I'll start with the searches to pinpoint the exact details. My plan is to address the query promptly, while being very clear in identifying the parameters. Then I'll synthesize and present the information in an accessible way. I am preparing the user's expectations.\n\n\n**Detailing Search Queries**\n\nI'm now fine-tuning the search strings. Considering the user's likely intent, I'm optimizing the queries to capture the most crucial information. This involves testing different permutations, like combining specs with \"Zen 5c\" and specifying the NPU and GPU details. I'll prioritize official AMD sources and reputable tech sites to ensure accuracy in identifying and collecting data points.\n\n\n**Gathering the Details**\n\nI'm now fully immersed in extracting the salient specifications. I've analyzed the simulated search results, organizing the data into a clear structure. The focus is on creating a concise \"Key Specifications\" table, including core counts, clock speeds, and architecture details for ease of understanding. I'll also be highlighting key information, like the NPU's TOPS, and explaining the implications of the hybrid core design.\n\n\n**Organizing the Data**\n\nI'm focusing on distilling the key details into a structured response. I have the specifications from the simulated search, and I'm now working on the \"Key Specifications\" table. It will have processor name, CPU and GPU details, and NPU performance, as I continue to think about the best way to make the information easily digestible and understandable. The explanation of hybrid cores and the significance of the NPU are also top of mind.\n\n\n**Synthesizing the Response**\n\nI'm now formulating the comprehensive response. I will begin by acknowledging the user's likely intent and clarifying the processor's name. The \"Key Specifications\" table is coming together, incorporating CPU, GPU, and NPU details. I am also drafting explanatory paragraphs to accompany the table, focusing on hybrid cores, NPU performance, and the implications for AI and overall system performance.\n\n\n**Constructing the Final Answer**\n\nI'm now formulating the full response, incorporating the clarified naming and a detailed \"Key Specifications\" table, derived from the hypothetical search. This includes core configurations and the NPU's impressive capabilities. I am providing the user with a very accurate and useful set of information. After this, I'll summarize the key points and provide a conclusion, including a timeframe for the product launch.\n\n\n**Refining the Structure**\n\nI'm now focusing on structuring the response. I will correct the naming error and present the detailed specifications in a well-organized format, specifically a \"Key Specifications\" table. Crucially, I plan to explain the hybrid core design and the role of the NPU. The ultimate goal is to provide clear and actionable information.\n\n\n",
                                                            "format" : "unknown"
                                                          } ],
                                                          "annotations" : [ {
                                                            "type" : "url_citation",
                                                            "url_citation" : {
                                                              "end_index" : 0,
                                                              "start_index" : 0,
                                                              "title" : "Intel® Xeon® Processor E5-1650 v3 (15M Cache, 3.50 GHz) - Product Specifications | Intel",
                                                              "url" : "https://www.intel.com/content/www/us/en/products/sku/82765/intel-xeon-processor-e51650-v3-15m-cache-3-50-ghz/specifications.html",
                                                              "content" : "- [Specifications](https://www.intel.com/content/www/us/en/products/sku/82765/intel-xeon-processor-e51650-v3-15m-cache-3-50-ghz/specifications.html) - [Ordering & Compliance](https://www.intel.com/content/www/us/en/products/sku/82765/intel-xeon-processor-e51650-v3-15m-cache-3-50-ghz/ordering.html) - [Support](https://www.intel.com/content/www/us/en/products/sku/82765/intel-xeon-processor-e51650-v3-15m-cache-3-50-ghz/support.html) All information provided is subject to change at any time, without notice. Intel may make changes to manufacturing life cycle, specifications, and product descriptions at any time, without notice."
                                                            }
                                                          }, {
                                                            "type" : "url_citation",
                                                            "url_citation" : {
                                                              "end_index" : 0,
                                                              "start_index" : 0,
                                                              "title" : "Intel® Xeon® Processor E5-1600/2400/2600/4600 (E5-Product Family) Product Families Datasheet- Volume Two",
                                                              "url" : "https://www.intel.com/content/dam/www/public/us/en/documents/datasheets/xeon-e5-1600-2600-vol-2-datasheet.pdf",
                                                              "content" : "2 Intel® Xeon® Processor E5-1600/2400/2600/4600 (E5-Product Family) Product Families Legal Lines and Disclaimers INFORMATION IN THIS DOCUMENT IS PROVIDED IN CONNECTION WITH INTEL® PRODUCTS. NO LICENSE, EXPRESS OR IMPLIED,  BY ESTOPPEL OR OTHERWISE, TO ANY INTELLECTUAL PROPERTY RIGHTS IS GRANTED BY THIS DOCUMENT. EXCEPT AS "
                                                            }
                                                          }, {
                                                            "type" : "url_citation",
                                                            "url_citation" : {
                                                              "end_index" : 0,
                                                              "start_index" : 0,
                                                              "title" : "英特尔® 产品规范",
                                                              "url" : "https://www.intel.cn/content/www/cn/zh/ark.html",
                                                              "content" : " 您可以使用几种方式轻松搜索整个 Intel.com 网站。  “Ice Lake”、Ice AND Lake、Ice OR Lake、Ice*  您也可以尝试使用以下快速链接查看最受欢迎搜索的结果。   2100 系列 IPU 适配器（最高支持 200GbE） "
                                                            }
                                                          }, {
                                                            "type" : "url_citation",
                                                            "url_citation" : {
                                                              "end_index" : 0,
                                                              "start_index" : 0,
                                                              "title" : "AMD Ryzen™ 9 5950X Desktop Processor",
                                                              "url" : "https://www.amd.com/en/products/processors/desktops/ryzen/5000-series/amd-ryzen-9-5950x.html",
                                                              "content" : "[AMD Website Accessibility Statement](https://www.amd.com/en/site-notifications/accessibility-statement.html)\n\n- Image Zoom\n\n\nAMD Ryzen™ 9 5950X. One processor that can game as well as it creates. - [Drivers & Support](https://www.amd.com/en/support/downloads/drivers.html/processors/ryzen/ryzen-5000-series/amd-ryzen-9-5950x.html)\n\n\n### 16 Cores. 0 Compromises. ### Drivers and Resources\n\n### [Driver & Software Downloads](https://www.amd.com/en/support/download/drivers.html)\n\nAccess the latest drivers, software and release notes for AMD products."
                                                            }
                                                          }, {
                                                            "type" : "url_citation",
                                                            "url_citation" : {
                                                              "end_index" : 0,
                                                              "start_index" : 0,
                                                              "title" : "Datasheet Vol. 2: Intel® Core™ i7 Processor Family for LGA2011-v3 Socket",
                                                              "url" : "https://www.intel.cn/content/dam/www/public/us/en/documents/datasheets/core-i7-6xxx-lga2011-v3-datasheet-vol-2.pdf",
                                                              "content" : "Supporting Desktop Intel® Core™ i7-6950X Extreme Edition Processor for  Supporting Desktop Intel® Core™ i7-6900K, i7-6850K, and i7-6800K  Legal Lines and Disclaimers UNLESS OTHERWISE AGREED IN WRITING BY INTEL, THE INTEL PRODUCTS ARE NOT DESIGNED NOR INTENDED FOR ANY APPLICATION IN WHICH THE FAILURE OF THE INTEL PRODUCT COULD CREATE A SITUATION WHERE PERSONAL INJURY OR DEATH MAY OCCUR. You may not use or facilitate the use of this document in connection with any infringement or other legal analysis concerning Intel  products described herein."
                                                            }
                                                          } ]
                                                        }
                                                      } ],
                                                      "usage" : {
                                                        "prompt_tokens" : 2298,
                                                        "completion_tokens" : 3099,
                                                        "total_tokens" : 5397
                                                      }
                                                    }
                                                    """u8.ToArray());
        // var completion = ModelReaderWriter.Read<ChatCompletion>(rawAudioResponse);
        // Assert.NotNull(completion);
        var jsonNode = await JsonNode.ParseAsync(rawAudioResponse.ToStream());
        if (jsonNode == null)
        {
            return;
        }

        List<ChatAnnotation> annotationList = new List<ChatAnnotation>();
        var choicesNode = jsonNode["choices"];
        int index = 0;
        if (choicesNode is JsonArray choicesArray)
        {
            //往往只有1个choice
            foreach (var choice in choicesArray)
            {
                if (choice is JsonObject choiceObject)
                {
                    /*var index = choiceObject["index"]?.GetValue<int>();
                    if (index == null)
                        continue;*/
                    var message = choiceObject["message"]?.AsObject();
                    if (message != null)
                    {
                        if (index == 0)
                        {
                            var annotations = message["annotations"]?.AsArray();
                            if (annotations != null)
                            {
                                foreach (var annotation in annotations)
                                {
                                    if (annotation is JsonObject annotationObject)
                                    {
                                        var annotationJson = annotationObject.ToJsonString();
                                        var annotationObj = JsonSerializer.Deserialize<ChatAnnotation>(annotationJson);
                                        if (annotationObj != null)
                                        {
                                            annotationList.Add(annotationObj);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                index++;
            }
        }

        output.WriteLine(annotationList.Count.ToString());
    }


    [Fact]
    public void SearchService()
    {
        var requestViewItem = new RequestViewItem("TODO")
        {
            SearchOption = new OpenRouterSearchOption(),
            TempAdditionalProperties = new AdditionalPropertiesDictionary()
            {
                {
                    "plugins", new OpenRouterSearchOption.PluginConfig[]
                    {
                        new OpenRouterSearchOption.PluginConfig()
                        {
                            Id = "web",
                            MaxResults = 5,
                            SearchPrompt = "AMD Ryzen AI 9 HX 370"
                        }
                    }
                }
            }
        };
        var serialize = JsonSerializer.Serialize(requestViewItem, FileBasedSessionBase.SerializerOption);
        output.WriteLine(serialize);
    }

    [Fact]
    public void ImageSupports()
    {
        var supportedFormats = MagickNET.SupportedFormats;

        // 过滤并打印出所有可读写的格式
        var readableFormats = supportedFormats
            .Where(f => f.SupportsReading && f.SupportsWriting)
            .Select(f => f.Format.ToString())
            .OrderBy(name => name)
            .ToList();

        output.WriteLine("ImageMagick.NET 支持的格式 (部分):");
        foreach (var format in readableFormats)
        {
            output.WriteLine(format);
        }
    }

    [Fact]
    public async Task StreamingAnnotation()
    {
        var options = new OpenAIClientOptions()
        {
            Endpoint = new Uri("https://openrouter.ai/api/v1"),
            Transport = new HttpClientPipelineTransport(new HttpClient()),
        };
        // options.AddPolicy();
        var environmentVariable = Environment.GetEnvironmentVariable("OpenRouterKEY");
        var chatClient = new OpenAIChatClientEx("google/gemini-2.5-pro-preview",
            new ApiKeyCredential(environmentVariable ?? throw new InvalidOperationException("需要设置环境变量OpenRouterKEY")),
            options);
        await foreach (var update in chatClient.CompleteChatStreamingAsync(new ChatMessage[]
                       {
                           new UserChatMessage("华为最早支持nfc的手机是什么型号？")
                       }, new ChatCompletionOptions()))
        {
            var chatMessageContent = update.ContentUpdate;
            if (chatMessageContent != null)
            {
                foreach (var contentPart in chatMessageContent)
                {
                    if (contentPart.Kind == ChatMessageContentPartKind.Text)
                    {
                        Debug.WriteLine(contentPart.Text);
                    }
                }
            }
        }
    }

    [Fact]
    [Experimental("SKEXP0001")]
    public void SearchPlugin()
    {
        var sqliteVectorStore = new SqliteVectorStore("", new SqliteVectorStoreOptions()
        {
            EmbeddingGenerator = new FakeEmbeddingGenerator()
        });
        var sqliteCollection = sqliteVectorStore.GetCollection<string, DocChunk>("test");
        var vectorStoreTextSearch = new VectorStoreTextSearch<DocChunk>(sqliteCollection);
        var kernelPlugin = vectorStoreTextSearch.CreateWithSearch("test", description: "des");
        foreach (var kernelFunction in kernelPlugin)
        {
            output.WriteLine(kernelFunction.Name);
            output.WriteLine(kernelFunction.Description);
        }
    }

    [Fact]
    public async Task TestImageSave()
    {
        // 创建并配置 BitmapImage
        var bitmapImage = new BitmapImage(new Uri("https://custom.typingmind.com/assets/models/openrouterai.png"));
        // 下载完成后保存图片
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmapImage));
        await using var fileStream = new FileStream("D:\\openrouterai.png", FileMode.Create);
        encoder.Save(fileStream);
    }

    [Fact]
    public void Random()
    {
        if (Uri.TryCreate("file:c:\\test.img", UriKind.RelativeOrAbsolute, out Uri? uri))
        {
            Debugger.Break();
        }
    }
}