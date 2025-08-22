using System.IO;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Input;
using LLMClient.Abstraction;
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
    private string _resourceName = string.Empty;
    private string? _errorMessage;
    private RagFileStatus _status = RagFileStatus.NotConstructed;

    [JsonPropertyName("Name")]
    public string ResourceName
    {
        get => _resourceName;
        set
        {
            if (value == _resourceName) return;
            _resourceName = value;
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

    [JsonIgnore]
    string IAIFunctionGroup.Name
    {
        get { return PluginName; }
    }

    [JsonIgnore] private string PluginName => string.Format("{1}{0}_Plugin", FileIndexInContext, FileType);

    [JsonIgnore]
    private string PluginDescription => string.Format("A plugin for File {0} information operations",
        this.ResourceName);

    [JsonIgnore]
    public string? AdditionPrompt
    {
        get { return $"{PluginName} is {PluginDescription}"; }
    }

    [JsonIgnore] public IReadOnlyList<AIFunction>? AvailableTools { get; }

    [JsonIgnore] public bool IsAvailable => IsInitialized && Status == RagFileStatus.Constructed;

    public string GetUniqueId()
    {
        return DocumentId;
    }

    public Task EnsureAsync(CancellationToken token)
    {
        return Task.CompletedTask;
    }

    public Guid Id { get; set; } = Guid.NewGuid();


    protected RagFileBase()
    {
        AvailableTools =
        [
            CreateQueryFunction(),
            CreateGetStructureFunction(),
            CreateGetFullDocumentFunction(),
            CreateGetSectionFunction()
        ];
    }

    protected RagFileBase(FileInfo fileInfo) : this()
    {
        FilePath = fileInfo.FullName;
        FileSize = fileInfo.Length;
        EditTime = fileInfo.LastWriteTime;
        ResourceName = fileInfo.Name;
    }

    public string FilePath { get; set; } = string.Empty;
    public DateTime EditTime { get; set; }
    public long FileSize { get; set; } = 0;

    public RagFileStatus Status
    {
        get => _status;
        set
        {
            if (value == _status) return;
            if (_status == RagFileStatus.Constructing)
            {
                ConstructionLogs.Stop();
            }

            _status = value;
            if (value == RagFileStatus.Constructing)
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
    private int _fileIndexInContext;
    private long _summaryTokensConsumption = 0;

    public ICommand SwitchConstructCommand => new ActionCommand(async o =>
    {
        if (Status == RagFileStatus.Constructing)
        {
            await StopConstruct();
        }
        else
        {
            if (Status == RagFileStatus.Constructed)
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

    public long SummaryTokensConsumption
    {
        get => _summaryTokensConsumption;
        set
        {
            if (value == _summaryTokensConsumption) return;
            _summaryTokensConsumption = value;
            OnPropertyChanged();
        }
    }

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
        if (Status == RagFileStatus.Constructing)
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
            Status = RagFileStatus.Constructing;
            await ConstructCore(cancellationToken);
            Status = RagFileStatus.Constructed;
        }
        catch (Exception e)
        {
            await DeleteAsync(cancellationToken);
            ConstructionLogs.LogError("构建过程中发生错误: {ErrorMessage}", e.Message);
            ErrorMessage = e.Message;
            Status = RagFileStatus.Error;
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

    /// <summary>
    /// allow custom query options by semantic kernel
    /// </summary>
    protected abstract KernelFunctionFromMethodOptions QueryOptions { get; }

    public ICommand ViewCommand => new ActionCommand(o =>
    {
        var window = new Window()
        {
            Content = new FileRagDataView()
            {
                DataContext = new FileRagDataViewModel(this)
            }
        };
        window.ShowDialog();
    });

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

            StructResult result = await this.QueryAsync(queryString, dynamicOptions, token);
            var matchResult = result.Nodes;
            matchResult.TryAddAdditionalFunctionCallResult();
            return matchResult.GetView();
        }

        return KernelFunctionFactory.CreateFromMethod(WrapQueryAsync, methodOptions);
    }

    public KernelFunction CreateGetStructureFunction()
    {
        var methodOptions = new KernelFunctionFromMethodOptions
        {
            FunctionName = "GetDocumentStructure",
            Description = "Get the document structure with summaries in a formatted string. \n" +
                          "Suggested to use this function before any other call.",
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
            var result = (StructResult)(await GetStructureAsync(token));
            return result.Nodes.GetStructure();
        }

        return KernelFunctionFactory.CreateFromMethod(WrapGetStructureAsync, methodOptions);
    }

    public KernelFunction CreateGetFullDocumentFunction()
    {
        var methodOptions = new KernelFunctionFromMethodOptions
        {
            FunctionName = "GetFullDocument",
            Description =
                "Get the entire document including the hierarchy, node summary and actual text in a formatted string as a tree.\n" +
                "Don't use this Do not use this function unless necessary.",
            Parameters = [],
            ReturnParameter = new KernelReturnParameterMetadata()
            {
                Description =
                    "The full content of the document.\n- Title 0\n  This is a paragraph.\n  This is another paragraph.\n- Title 1\n  - Title 1.1\n    This is a paragraph under Title 1.1.\n    This is another paragraph under Title 1.1.\n  - Title 1.2\n    This is a paragraph under Title 1.2.\n    This is another paragraph under Title 1.2.\n- Title 2\n  This is a paragraph under Title 2.\n  This is another paragraph under Title 2.",
                ParameterType = typeof(string)
            }
        };

        async Task<string> WrapGetFullDocumentAsync(Kernel kernel, KernelFunction function, KernelArguments arguments,
            CancellationToken token)
        {
            var result = (StructResult)(await GetFullDocumentAsync(token));
            var resultNodes = result.Nodes;
            resultNodes.TryAddAdditionalFunctionCallResult();
            return resultNodes.GetView();
        }

        return KernelFunctionFactory.CreateFromMethod(WrapGetFullDocumentAsync, methodOptions);
    }

    public KernelFunction CreateGetSectionFunction()
    {
        var methodOptions = new KernelFunctionFromMethodOptions
        {
            FunctionName = "GetSection",
            Description = "Get the content of a specific section in the document.\n" +
                          "Warning: You should get the structure of the document first before using this function",
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

            var result = (StructResult)(await GetSectionAsync(sectionName.ToString()!, token));
            var resultNodes = result.Nodes;
            if (resultNodes.Any())
            {
                resultNodes.TryAddAdditionalFunctionCallResult();
                return resultNodes.GetView();
            }

            return string.Empty;
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
public class StructResult : ISearchResult
{
    public StructResult(IList<ChunkNode> nodes)
    {
        Nodes = nodes;
    }

    public string? DocumentId { get; set; }

    public IList<ChunkNode> Nodes { get; set; }
}

/// <summary>
/// 字符串查询结果
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