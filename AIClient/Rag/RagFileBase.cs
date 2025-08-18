using System.ComponentModel;
using System.IO;
using System.Text;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Input;
using LLMClient.Abstraction;
using LLMClient.Data;
using LLMClient.Dialog;
using LLMClient.Endpoints;
using LLMClient.UI;
using LLMClient.UI.Log;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.Xaml.Behaviors.Core;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace LLMClient.Rag;

public abstract class RagFileBase : BaseViewModel, IRagFileSource
{
    private string _name = string.Empty;
    private string? _errorMessage;
    private ConstructStatus _status = ConstructStatus.NotConstructed;

    public string Name
    {
        get => _name;
        set
        {
            if (value == _name) return;
            _name = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// 在上下文中该文件的索引，由于KernelPlugin的命名限制，函数不能直接使用文件名命名。
    /// </summary>
    [JsonPropertyName("FileIndex")]
    public int FileIndexInContext
    {
        get => _fileIndexInContext;
        set
        {
            if (value == _fileIndexInContext) return;
            _fileIndexInContext = value;
            OnPropertyChanged();
            OnPropertyChanged(PluginName);
        }
    }

    [JsonIgnore] private string PluginName => string.Format("File{0}_{1}_Plugin", FileIndexInContext, FileType);

    [JsonIgnore]
    private string PluginDescription => string.Format("A plugin for File {0} operations",
        Name);

    [JsonIgnore]
    public string? AdditionPrompt
    {
        get { return $"{PluginName} is {PluginDescription}"; }
    }

    [JsonIgnore]
    public IReadOnlyList<AIFunction>? AvailableTools
    {
        get => _availableTools;
        private set
        {
            if (Equals(value, _availableTools)) return;
            _availableTools = value;
            OnPropertyChanged();
        }
    }

    [JsonIgnore] public bool IsAvailable => IsInitialized && Status == ConstructStatus.Constructed;

    public string GetUniqueId()
    {
        return DocumentId;
    }

    public Task EnsureAsync(CancellationToken token)
    {
        //由于plugin的名称可能动态变化，因此每次都需要重新创建。
        var kernelPlugin = KernelPluginFactory.CreateFromFunctions(PluginName, PluginDescription, _functions);
#pragma warning disable SKEXP0001
        AvailableTools = kernelPlugin.AsAIFunctions().ToArray();
#pragma warning restore SKEXP0001
        return Task.CompletedTask;
    }

    public Guid Id { get; set; } = Guid.NewGuid();

    private KernelFunction[] _functions;

    protected RagFileBase()
    {
        _functions =
        [
            CreateQueryFunction(),
            CreateGetStructureFunction(),
            CreateGetDocumentFunction(),
            CreateGetSectionFunction()
        ];
    }

    protected RagFileBase(FileInfo fileInfo) : this()
    {
        FilePath = fileInfo.FullName;
        FileSize = fileInfo.Length;
        EditTime = fileInfo.LastWriteTime;
        Name = fileInfo.Name;
    }

    public string FilePath { get; set; } = string.Empty;
    public DateTime EditTime { get; set; }
    public long FileSize { get; set; } = 0;

    public ConstructStatus Status
    {
        get => _status;
        set
        {
            if (value == _status) return;
            if (_status == ConstructStatus.Constructing)
            {
                ConstructionLogs.Stop();
            }

            _status = value;
            if (value == ConstructStatus.Constructing)
            {
                ConstructionLogs.Start();
            }

            OnPropertyChanged();
            OnPropertyChanged(nameof(IsAvailable));
        }
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        set
        {
            if (value == _errorMessage) return;
            _errorMessage = value;
            OnPropertyChanged();
        }
    }

    [JsonIgnore] public LogsViewModel ConstructionLogs { get; set; } = new LogsViewModel();

    [JsonIgnore] public abstract DocumentFileType FileType { get; }

    [JsonIgnore]
    public virtual string DocumentId
    {
        get { return $"{FileType}_{Id}"; }
    }

    [JsonIgnore]
    public bool IsInitialized
    {
        get => _isInitialized;
        protected set
        {
            if (value == _isInitialized) return;
            _isInitialized = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsAvailable));
        }
    }

    /*public ICommand ClearEmbeddingCommand => new ActionCommand(async o =>
    {
        if (Status == ConstructStatus.Constructing)
        {
            return;
        }

        if (MessageBox.Show("是否要清空数据？", "提示",
                MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No,
                MessageBoxOptions.DefaultDesktopOnly) != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            await DeleteAsync();
            MessageEventBus.Publish("数据已清空");
        }
        catch (Exception e)
        {
            MessageBox.Show($"删除数据失败: {e.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    });*/

    private CancellationTokenSource? _constructionCancellationTokenSource;
    private Task? _constructionTask;
    private bool _isInitialized;
    private IReadOnlyList<AIFunction>? _availableTools;
    private int _fileIndexInContext;

    public ICommand SwitchConstructCommand => new ActionCommand(async o =>
    {
        if (Status == ConstructStatus.Constructing)
        {
            await StopConstruct();
        }
        else
        {
            if (Status == ConstructStatus.Constructed)
            {
                if (MessageBox.Show("是否要重新构建？", "提示",
                        MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No,
                        MessageBoxOptions.DefaultDesktopOnly) != MessageBoxResult.Yes)
                {
                    return;
                }
            }

            _constructionCancellationTokenSource = new CancellationTokenSource();
            _constructionTask = ConstructAsync(_constructionCancellationTokenSource.Token);
            try
            {
                await _constructionTask;
            }
            finally
            {
                _constructionCancellationTokenSource?.Dispose();
                _constructionCancellationTokenSource = null;
            }
        }
    });

    public long SummaryTokensConsumption { get; set; } = 0;

    public virtual Task InitializeAsync()
    {
        if (IsInitialized)
        {
            return Task.CompletedTask;
        }

        IsInitialized = true;
        return Task.CompletedTask;
    }

    public virtual async Task ConstructAsync(CancellationToken cancellationToken = default)
    {
        if (Status == ConstructStatus.Constructing)
        {
            // Already constructing, no need to construct again.
            return;
        }

        try
        {
            ErrorMessage = null;
            //must ensure the file is deleted before constructing again.
            await DeleteAsync(cancellationToken);
            // await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
            Status = ConstructStatus.Constructing;
            await ConstructCore(cancellationToken);
            Status = ConstructStatus.Constructed;
        }
        catch (Exception e)
        {
            ConstructionLogs.LogError("构建过程中发生错误: {ErrorMessage}", e.Message);
            ErrorMessage = e.Message;
            Status = ConstructStatus.Error;
            await DeleteAsync(cancellationToken);
        }
        finally
        {
            
        }
    }

    public async Task StopConstruct()
    {
        if (_constructionCancellationTokenSource != null && _constructionTask != null)
        {
            await _constructionCancellationTokenSource.CancelAsync();
            try
            {
                await _constructionTask;
            }
            catch (OperationCanceledException)
            {
                // Expected, suppress to prevent unhandled exception.
            }
        }
    }

    public abstract Task DeleteAsync(CancellationToken cancellationToken = default);

    protected abstract Task ConstructCore(CancellationToken cancellationToken = default);

    public abstract Task<ISearchResult> QueryAsync(string query, dynamic options,
        CancellationToken cancellationToken = default);

    public abstract Task<ISearchResult> GetStructureAsync(CancellationToken cancellationToken = default);

    public abstract Task<ISearchResult> GetSectionAsync(string sectionName,
        CancellationToken cancellationToken = default);

    public abstract Task<ISearchResult> GetFullDocumentAsync(CancellationToken cancellationToken = default);

    private const int SummaryTrigger = 3072; // 摘要触发长度

    protected Func<string, CancellationToken, Task<string>> CreateLLMCall(ILLMChatClient client,
        SemaphoreSlim clientSemaphore, PromptsCache cache, int summarySize = 1024,
        int retryCount = 3, ILogger? logger = null)
    {
        client.Parameters.Streaming = false;
        return async (content, token) =>
        {
            if (string.IsNullOrEmpty(content))
            {
                logger?.LogInformation("内容为空，不进行摘要。");
                return string.Empty;
            }

            if (content.Length < SummaryTrigger)
            {
                logger?.LogInformation("内容长度未超过{SummaryTrigger}，不进行摘要。", SummaryTrigger);
                return content;
            }

            if (cache?.TryGetValue(content, out var result) == true)
            {
                return result;
            }

            var response = new CompletedResult();
            await clientSemaphore.WaitAsync(token);
            try
            {
                var stringBuilder = new StringBuilder(
                    $"Provide a concise and complete summarization of the following text blocks that does not exceed {summarySize} words. " +
                    "\nThis summary must always:" +
                    "\n- Use the same language of the text blocks" +
                    "\n- Focus on the most significant aspects of the text blocks\n" +
                    "\n- Include details from any existing summary" +
                    "\nThis summary must never:" +
                    "\n- Critique, correct, interpret, presume, or assume" +
                    "\n- Identify faults, mistakes, misunderstanding, or correctness" +
                    "\n- Analyze what has not occurred" +
                    "\n- Exclude details from any existing summary" +
                    "\n\nPlease summarize the following text blocks until end:\n\n");
                /*var stringBuilder = new StringBuilder("请为以下内容生成一个摘要，要求：\r\n" +
                                                      "1. 首先判断原文使用的语言，摘要使用的语言必须和原文一致。\r\n" +
                                                      "2. 摘要长度不应超过" + summarySize + "个字。\r\n" +
                                                      "3. 摘要内容应包含原文的主要信息。\r\n" +
                                                      "4. 摘要应尽量简洁明了。\r\n" +
                                                      "5. 如果原文是多段落的内容，摘要应包含每个段落的主要信息。\r\n" +
                                                      "6. 摘要内容均来自于原文，禁止联想或掺入个人喜好。\r\n");*/
                stringBuilder.Append(content);
                var dialogContext = new DialogContext(new[]
                {
                    new RequestViewItem() { TextMessage = stringBuilder.ToString(), }
                });
                int tryCount = 0;

                while (tryCount < retryCount)
                {
                    response = await client.SendRequest(dialogContext, token);
                    tryCount++;
                    SummaryTokensConsumption += response.Usage?.TotalTokenCount ?? 0;
                    var textResponse = response.TextResponse;
                    if (!string.IsNullOrEmpty(textResponse) && !response.IsInterrupt)
                    {
                        cache?.TryAdd(content, textResponse);
                        return textResponse;
                    }
                }
            }
            finally
            {
                clientSemaphore.Release();
            }

            throw new InvalidOperationException("LLM response failed after " + retryCount + " attempts. error: " +
                                                response.ErrorMessage);
        };
    }

    /// <summary>
    /// allow custom query options by semantic kernel
    /// </summary>
    protected abstract KernelFunctionFromMethodOptions QueryOptions { get; }

    public KernelFunction CreateQueryFunction()
    {
        var methodOptions = QueryOptions;

        async Task<string> WrapQueryAsync(Kernel kernel, KernelFunction function, KernelArguments arguments,
            CancellationToken token)
        {
            arguments.TryGetValue("query", out var query);
            var queryString = query?.ToString();
            if (string.IsNullOrEmpty(queryString))
            {
                return string.Empty;
            }

            dynamic dynamicOptions = new System.Dynamic.ExpandoObject();
            if (methodOptions.Parameters != null)
            {
                foreach (var parameter in methodOptions.Parameters)
                {
                    if (arguments.TryGetValue(parameter.Name, out var value) && value != null)
                    {
                        // 将参数值添加到动态选项中
                        ((IDictionary<string, object>)dynamicOptions)[parameter.Name] = value;
                    }
                    else if (parameter.IsRequired)
                    {
                        throw new ArgumentException($"Missing required parameter: {parameter.Name}");
                    }
                }
            }

            var result = (StringQueryResult)(await this.QueryAsync(queryString, dynamicOptions, token));
            return result.FormattedResult;
        }

        return KernelFunctionFactory.CreateFromMethod(WrapQueryAsync, methodOptions);
    }

    public KernelFunction CreateGetStructureFunction()
    {
        var methodOptions = new KernelFunctionFromMethodOptions
        {
            FunctionName = "GetDocumentStructure",
            Description = "Get the document structure in a formatted string.",
            Parameters = [],
            ReturnParameter = new KernelReturnParameterMetadata()
            {
                Description =
                    "Doc structure:\n- Title 0\n  Summary: Summary 0\n- Title 1\n  Summary: Summary 1\n  - Title 1.1\n    Summary: Summary 1.1\n  - Title 1.2\n    Summary: Summary 1.2\n- Title 2\n  Summary: Summary 2",
                ParameterType = typeof(string)
            }
        };

        async Task<string> WrapGetStructureAsync(Kernel kernel, KernelFunction function, KernelArguments arguments,
            CancellationToken token)
        {
            var result = (StringQueryResult)(await GetStructureAsync(token));
            return result.FormattedResult;
        }

        return KernelFunctionFactory.CreateFromMethod(WrapGetStructureAsync, methodOptions);
    }

    public KernelFunction CreateGetDocumentFunction()
    {
        var methodOptions = new KernelFunctionFromMethodOptions
        {
            FunctionName = "GetDocument",
            Description = "Get the document content in a formatted string.",
            Parameters = [],
            ReturnParameter = new KernelReturnParameterMetadata()
            {
                Description =
                    "The full content of the document.\n- Title 0\n  This is a paragraph.\n  This is another paragraph.\n- Title 1\n  - Title 1.1\n    This is a paragraph under Title 1.1.\n    This is another paragraph under Title 1.1.\n  - Title 1.2\n    This is a paragraph under Title 1.2.\n    This is another paragraph under Title 1.2.\n- Title 2\n  This is a paragraph under Title 2.\n  This is another paragraph under Title 2.",
                ParameterType = typeof(string)
            }
        };

        async Task<string> WrapGetDocumentAsync(Kernel kernel, KernelFunction function, KernelArguments arguments,
            CancellationToken token)
        {
            var result = (StringQueryResult)(await GetFullDocumentAsync(token));
            return result.FormattedResult;
        }

        return KernelFunctionFactory.CreateFromMethod(WrapGetDocumentAsync, methodOptions);
    }

    public KernelFunction CreateGetSectionFunction()
    {
        var methodOptions = new KernelFunctionFromMethodOptions
        {
            FunctionName = "GetSection",
            Description = "Get the content of a specific section in the document.",
            Parameters =
            [
                new KernelParameterMetadata("sectionName")
                {
                    Description = "The name of the section to retrieve.",
                    ParameterType = typeof(string),
                    IsRequired = true
                }
            ],
            ReturnParameter = new KernelReturnParameterMetadata()
            {
                Description =
                    "The content of the specified section. Example: \n- Title 0\n  This is a paragraph.\n  This is another paragraph.",
                ParameterType = typeof(string)
            }
        };

        async Task<string> WrapGetSectionAsync(Kernel kernel, KernelFunction function, KernelArguments arguments,
            CancellationToken token)
        {
            if (!arguments.TryGetValue("sectionName", out var sectionName) ||
                string.IsNullOrEmpty(sectionName?.ToString()))
            {
                throw new ArgumentException("Missing required parameter: sectionName");
            }

            var result = (StringQueryResult)(await GetSectionAsync(sectionName.ToString()!, token));
            return result.FormattedResult;
        }

        return KernelFunctionFactory.CreateFromMethod(WrapGetSectionAsync, methodOptions);
    }

    public object Clone()
    {
        var serialize = JsonSerializer.Serialize(this, Extension.DefaultJsonSerializerOptions);
        return JsonSerializer.Deserialize(serialize, GetType(), Extension.DefaultJsonSerializerOptions)!;
    }
}

/// <summary>
/// 可表示带锁缩进的结构化查询结果。
/// <para> 形式： Title
///                —— SubTitle1
///                —— SubTitle2
///                     Paragraphs
/// </para>
/// </summary>
public class StringQueryResult : ISearchResult
{
    public StringQueryResult(string formattedResult)
    {
        FormattedResult = formattedResult;
    }

    public string? DocumentId { get; set; }

    public string FormattedResult { get; set; }
}