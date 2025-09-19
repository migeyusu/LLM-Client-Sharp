using System.Diagnostics.CodeAnalysis;
using System.Dynamic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Google.Apis.Services;
using LLMClient.Abstraction;
using LLMClient.Rag;
using LLMClient.UI;
using LLMClient.UI.Component;
using MaterialDesignThemes.Wpf;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Data;
using Microsoft.SemanticKernel.Plugins.Web.Google;

namespace LLMClient.MCP.Servers;

public class GoogleSearchPlugin : BaseViewModel, IRagSource, ISearchOption
{
    private IReadOnlyList<AIFunction>? _availableTools;

    private const string KernelPluginName = "GoogleTextSearch";

    public object Clone()
    {
        return Activator.CreateInstance(this.GetType())!;
    }

    [JsonIgnore] public string Name { get; } = KernelPluginName;

    [JsonIgnore] public ThemedIcon Icon => LocalThemedIcon.FromPackIcon(PackIconKind.Google);

    public string? AdditionPrompt => $"{this.Name} enables performing web searches using Google. ";

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

    public bool IsAvailable => AvailableTools?.Count > 0;

    public string GetUniqueId()
    {
        return $"{this.GetType().FullName}";
    }

    private GoogleSearchOption? _config;

#pragma warning disable SKEXP0050
    private GoogleTextSearch? _textSearch;
#pragma warning restore SKEXP0050

    public async Task EnsureAsync(CancellationToken token)
    {
        var config = ServiceLocator.GetService<GlobalOptions>()?.GoogleSearchOption;
        if (config?.IsValid() == true && !config.Equals(_config))
        {
#pragma warning disable SKEXP0050
            _textSearch = new GoogleTextSearch(
                initializer: new BaseClientService.Initializer
                    { ApiKey = config.ApiKey }, config.SearchEngineId);
#pragma warning restore SKEXP0050
#pragma warning disable SKEXP0001
            this.AvailableTools = [CreateGetSearchResults(_textSearch)];
#pragma warning restore SKEXP0001
            _config = config;
        }
    }

    [Experimental("SKEXP0001")]
    private static KernelFunction CreateGetSearchResults(ITextSearch textSearch,
        KernelFunctionFromMethodOptions? options = null,
        TextSearchOptions? searchOptions = null)
    {
        async Task<IEnumerable<object>> GetSearchResultAsync(Kernel kernel, KernelFunction function,
            KernelArguments arguments, CancellationToken cancellationToken, int count = 10, int skip = 0)
        {
            arguments.TryGetValue("query", out var query);
            if (string.IsNullOrEmpty(query?.ToString()))
            {
                return [];
            }

            if (arguments.TryGetValue("count", out var countObj) && countObj is int countVal and > 0)
            {
                count = countVal;
            }

            if (arguments.TryGetValue("skip", out var skipObj) && skipObj is int skipVal and >= 0)
            {
                skip = skipVal;
            }

            searchOptions ??= new()
            {
                Top = count,
                Skip = skip,
                Filter = CreateBasicFilter(options, arguments)
            };

            var result = await textSearch.GetSearchResultsAsync(query?.ToString()!, searchOptions, cancellationToken)
                .ConfigureAwait(false);
            return await result.Results.ToListAsync(cancellationToken).ConfigureAwait(false);
        }

        options ??= DefaultGetSearchResultsMethodOptions();
        return KernelFunctionFactory.CreateFromMethod(
            GetSearchResultAsync,
            options);
    }

    /// <summary>
    /// Create the default <see cref="KernelFunctionFromMethodOptions"/> for <see cref="ITextSearch.GetSearchResultsAsync(string, TextSearchOptions?, CancellationToken)"/>.
    /// </summary>
    [RequiresUnreferencedCode(
        "Uses reflection for generating JSON schema for method parameters and return type, making it incompatible with AOT scenarios.")]
    [RequiresDynamicCode(
        "Uses reflection for generating JSON schema for method parameters and return type, making it incompatible with AOT scenarios.")]
    private static KernelFunctionFromMethodOptions DefaultGetSearchResultsMethodOptions() =>
        new()
        {
            FunctionName = "GetSearchResults",
            Description = "Perform a search for content related to the specified query.",
            Parameters = GetDefaultKernelParameterMetadata(),
            ReturnParameter = new() { ParameterType = typeof(KernelSearchResults<TextSearchResult>) },
        };

    private static IEnumerable<KernelParameterMetadata>? _sKernelParameterMetadata;

    [RequiresUnreferencedCode(
        "Uses reflection for generating JSON schema for method parameters and return type, making it incompatible with AOT scenarios.")]
    [RequiresDynamicCode(
        "Uses reflection for generating JSON schema for method parameters and return type, making it incompatible with AOT scenarios.")]
    private static IEnumerable<KernelParameterMetadata> GetDefaultKernelParameterMetadata()
    {
        return _sKernelParameterMetadata ??=
        [
            new KernelParameterMetadata("query")
                { Description = "What to search for", ParameterType = typeof(string), IsRequired = true },
            new KernelParameterMetadata("count")
            {
                Description = "Number of results", ParameterType = typeof(int), IsRequired = false, DefaultValue = 2
            },
            new KernelParameterMetadata("skip")
            {
                Description = "Number of results to skip", ParameterType = typeof(int), IsRequired = false,
                DefaultValue = 0
            },
        ];
    }

    /// <summary>
    /// Create a <see cref="TextSearchFilter" /> for the search based on any additional parameters included in the <see cref="KernelFunctionFromMethodOptions"/>
    /// </summary>
    /// <param name="options">Kernel function method options.</param>
    /// <param name="arguments">Kernel arguments.</param>
    [Experimental("SKEXP0001")]
    private static TextSearchFilter? CreateBasicFilter(KernelFunctionFromMethodOptions? options,
        KernelArguments arguments)
    {
        if (options?.Parameters is null)
        {
            return null;
        }

        TextSearchFilter? filter = null;
        foreach (var parameter in options.Parameters)
        {
            // treat non standard parameters as equality filter clauses
            if (!parameter.Name.Equals("query", System.StringComparison.Ordinal) &&
                !parameter.Name.Equals("count", System.StringComparison.Ordinal) &&
                !parameter.Name.Equals("skip", System.StringComparison.Ordinal))
            {
                if (arguments.TryGetValue(parameter.Name, out var value) && value is not null)
                {
                    filter ??= new TextSearchFilter();
                    filter.Equality(parameter.Name, value);
                }
            }
        }

        return filter;
    }

    public bool CheckCompatible(ILLMChatClient client)
    {
        return client.Model.SupportFunctionCall;
    }

    public async Task ApplySearch(DialogContext context)
    {
        var requestViewItem = context.Request;
        if (requestViewItem == null)
        {
            return;
        }

        requestViewItem.FunctionGroups ??= [];
        if (requestViewItem.FunctionGroups.Any(tree =>
                AIFunctionGroupComparer.Instance.Equals(tree.Data, this)))
        {
            return;
        }

        var functionGroupTree = new CheckableFunctionGroupTree(this);
        await functionGroupTree.EnsureAsync(CancellationToken.None);
        functionGroupTree.IsSelected = true;
        requestViewItem.FunctionGroups.Add(functionGroupTree);
    }

    public async Task<ISearchResult> QueryAsync(string query, dynamic options,
        CancellationToken cancellationToken = default)
    {
        if (_textSearch == null)
        {
            throw new InvalidOperationException(
                "Google Search is not initialized. Please configure the API key and Search Engine ID first.");
        }

        TextSearchOptions textSearchOptions;
        if (options == null)
        {
            textSearchOptions = new TextSearchOptions();
        }
        else if (options is TextSearchOptions tso)
        {
            textSearchOptions = tso;
        }
        else if (options is ExpandoObject expandoObject)
        {
            textSearchOptions = ConvertExpandoToTextSearchOptions(expandoObject);
        }
        else
        {
            // 对于其他 dynamic 类型，尝试通过反射获取属性
            textSearchOptions = ConvertDynamicToTextSearchOptions(options);
        }

#pragma warning disable SKEXP0050
        var results = await _textSearch.GetTextSearchResultsAsync(query, textSearchOptions, cancellationToken);
        return new SKTextSearchResult(results.Results);
#pragma warning restore SKEXP0050
    }

    TextSearchOptions ConvertExpandoToTextSearchOptions(ExpandoObject expandoObject)
    {
        var dict = expandoObject as IDictionary<string, object>;
        int skipValue = 0;
        if (dict.TryGetValue("Skip", out var count) && count is int countValue)
        {
            skipValue = countValue;
        }

        int topValue = TextSearchOptions.DefaultTop;
        if (dict.TryGetValue("Top", out var offset) && offset is int offsetValue)
        {
            topValue = offsetValue;
        }

        return new TextSearchOptions() { Skip = skipValue, Top = topValue };
    }

    private static TextSearchOptions ConvertDynamicToTextSearchOptions(dynamic dynamicOptions)
    {
        try
        {
            // 处理匿名对象等其他 dynamic 类型
            var type = dynamicOptions.GetType();
            int skipValue = 0;
            if (type.GetProperty("Skip")?.GetValue(dynamicOptions) is int skip)
            {
                skipValue = skip;
            }

            int topValue = TextSearchOptions.DefaultTop;
            if (type.GetProperty("Top")?.GetValue(dynamicOptions) is int top)
            {
                topValue = top;
            }

            return new TextSearchOptions() { Skip = skipValue, Top = topValue };
        }
        catch
        {
            return new TextSearchOptions();
        }
    }

    public string ResourceName => KernelPluginName;
    public Guid Id { get; } = Guid.Parse("d3b5f8e2-1c4e-4f3a-9f7b-2e8f4c6d5a1b");
    public RagStatus Status { get; } = RagStatus.Constructed;

    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    public Task ConstructAsync(CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException();
    }

    public Task DeleteAsync(CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException();
    }
}

public class SKTextSearchResult : ISearchResult
{
    public SKTextSearchResult(IAsyncEnumerable<TextSearchResult> results)
    {
        Results = results;
    }

    public IAsyncEnumerable<TextSearchResult> Results { get; set; }
}