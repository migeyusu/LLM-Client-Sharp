using System.Text.Json.Serialization;
using Google.Apis.Services;
using LLMClient.Abstraction;
using LLMClient.UI;
using LLMClient.UI.Component;
using MaterialDesignThemes.Wpf;
using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Data;
using Microsoft.SemanticKernel.Plugins.Web.Google;

namespace LLMClient.MCP.Servers;

public class GoogleSearchPlugin : BaseViewModel, IAIFunctionGroup, ISearchService
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

    public async Task EnsureAsync(CancellationToken token)
    {
        var config = (await GlobalOptions.LoadOrCreate()).GoogleSearchOption;
        if (config.IsValid() && !config.Equals(_config))
        {
#pragma warning disable SKEXP0050
            var textSearch = new GoogleTextSearch(
                initializer: new BaseClientService.Initializer
                    { ApiKey = config.ApiKey }, config.SearchEngineId);
#pragma warning restore SKEXP0050
#pragma warning disable SKEXP0001
            this.AvailableTools = [textSearch.CreateGetTextSearchResults()];
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
        if (requestViewItem.FunctionGroups.Any((tree => AIFunctionGroupComparer.Instance.Equals(tree.Data, this))))
        {
            return;
        }

        var functionGroupTree = new CheckableFunctionGroupTree(this);
        await functionGroupTree.EnsureAsync(CancellationToken.None);
        functionGroupTree.IsSelected = true;
        requestViewItem.FunctionGroups.Add(functionGroupTree);
    }
}