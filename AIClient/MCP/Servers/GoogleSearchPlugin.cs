using System.Dynamic;
using System.Text.Json.Serialization;
using Google.Apis.Services;
using LLMClient.Abstraction;
using LLMClient.Rag;
using LLMClient.UI;
using LLMClient.UI.Component;
using MaterialDesignThemes.Wpf;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
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
            this.AvailableTools = [_textSearch.CreateGetTextSearchResults()];
#pragma warning restore SKEXP0001
            _config = config;
        }
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

    TextSearchOptions ConvertDynamicToTextSearchOptions(dynamic dynamicOptions)
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