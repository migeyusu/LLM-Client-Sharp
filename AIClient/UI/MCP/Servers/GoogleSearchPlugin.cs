using System.Text.Json.Serialization;
using Google.Apis.Services;
using LLMClient.Abstraction;
using LLMClient.UI.Component;
using MaterialDesignThemes.Wpf;
using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel.Data;
using Microsoft.SemanticKernel.Plugins.Web.Google;

namespace LLMClient.UI.MCP.Servers;

public class GoogleSearchPlugin : BaseViewModel, IBuiltInFunctionGroup, ISearchService
{
    private IReadOnlyList<AIFunction>? _availableTools;

    private const string KernelPluginName = "GoogleTextSearch";

    public GoogleSearchPlugin()
    {
    }

    public object Clone()
    {
        return Activator.CreateInstance(this.GetType())!;
    }

    [JsonIgnore] public string Name { get; } = KernelPluginName;
    
    [JsonIgnore]
    public ThemedIcon Icon => LocalThemedIcon.FromPackIcon(PackIconKind.Google);
    public string? AdditionPrompt { get; } = null;

    [JsonIgnore]
    public IReadOnlyList<AIFunction>? AvailableTools
    {
        get => _availableTools;
        private set
        {
            if (Equals(value, _availableTools)) return;
            _availableTools = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsToolAvailable));
        }
    }

    public bool IsToolAvailable
    {
        get { return AvailableTools is { Count: > 0 }; }
    }

    public string GetUniqueId()
    {
        return $"{this.GetType().FullName}";
    }

    private GoogleSearchOption? _config;

    public async Task EnsureAsync(CancellationToken token)
    {
        var config = (await GlobalOptions.LoadOrCreate()).GoogleSearchOption;
        if (config.IsValid && !config.Equals(_config))
        {
#pragma warning disable SKEXP0050
            var textSearch = new GoogleTextSearch(
                initializer: new BaseClientService.Initializer
                    { ApiKey = config.ApiKey! }, config.SearchEngineId!);
#pragma warning restore SKEXP0050
            var kernelPlugin = textSearch.CreateWithGetTextSearchResults(KernelPluginName);
#pragma warning disable SKEXP0001
            this.AvailableTools = kernelPlugin.AsAIFunctions().ToArray();
#pragma warning restore SKEXP0001
            _config = config;
        }
    }

    public bool CheckCompatible(ILLMModel model)
    {
        return model.SupportFunctionCall;
    }

    public Task ApplySearch(DialogContext context)
    {
        var requestViewItem = context.Request;
        if (requestViewItem == null)
        {
            return Task.CompletedTask;
        }

        requestViewItem.FunctionGroups ??= [];
        requestViewItem.FunctionGroups.Add(this);
        return Task.CompletedTask;
    }
}