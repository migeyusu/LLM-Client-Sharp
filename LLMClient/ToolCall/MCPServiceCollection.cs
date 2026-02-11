using System.Collections;
using System.Collections.ObjectModel;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using LLMClient.Abstraction;
using LLMClient.Component.Utility;
using LLMClient.Component.ViewModel.Base;
using LLMClient.ToolCall.Servers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Xaml.Behaviors.Core;

namespace LLMClient.ToolCall;

/// <summary>
/// 经过慎重考虑，设置为单例，因为mcp服务可能由客户端命令启动，重复启动会清空实例的上下文状态
/// </summary>
public class McpServiceCollection : BaseViewModel, IMcpServiceCollection, IFunctionGroupSource, IAsyncDisposable
{
    public const string FileName = "mcp_servers.json";

    private McpServerItem? _selectedServerItem;
    private ObservableCollection<McpServerItem> _items = new ObservableCollection<McpServerItem>();
    private bool _isInitialized;

    public ObservableCollection<McpServerItem> Items
    {
        get => _items;
        set
        {
            if (Equals(value, _items)) return;
            _items = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ImportFromJsonCommand));
            OnPropertyChanged(nameof(AddNewCommand));
            OnPropertyChanged(nameof(SaveCommand));
        }
    }

    public IEnumerable<McpServerItem> EnabledServers => _items.Where(item => item.IsEnabled);

    private bool _isLoaded;

    public bool IsLoaded
    {
        get => _isLoaded;
        set
        {
            if (value == _isLoaded) return;
            _isLoaded = value;
            OnPropertyChanged();
        }
    }

    public bool IsInitialized
    {
        get => _isInitialized;
        private set
        {
            if (value == _isInitialized) return;
            _isInitialized = value;
            OnPropertyChanged();
        }
    }

    public ICommand ImportFromJsonCommand { get; }

    public ICommand CopySelectedItemCommand { get; }

    public ICommand AddNewCommand { get; }

    [JsonIgnore]
    public McpServerItem? SelectedServerItem
    {
        get => _selectedServerItem;
        set
        {
            if (Equals(value, _selectedServerItem)) return;
            _selectedServerItem = value;
            OnPropertyChanged();
        }
    }

    public ICommand SaveCommand { get; }

    public ICommand RefreshToolsCommand { get; }

    public ICommand ReloadCommand { get; }

    public McpServiceCollection()
    {
        ImportFromJsonCommand = new RelayCommand(async () =>
        {
            var jsonContent =
                "{\n \"code-analysis\": {\n      \"command\": \"uv\",\n      \"args\": [\n        \"--directory\",\n        \"/PATH/TO/YOUR/REPO\",\n        \"run\",\n        \"code_analysis.py\"\n      ]\n    } \n}";
            while (true)
            {
                //只适配claude/jetbrains的mcp.json
                var jsonPreviewWindow = new JsonEditorWindow()
                {
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    JsonContent = jsonContent,
                };
                if (jsonPreviewWindow.ShowDialog() != true) return;
                try
                {
                    jsonContent = jsonPreviewWindow.JsonContent;
                    var jsonDocument = JsonNode.Parse(jsonContent);
                    if (jsonDocument != null)
                    {
                        var name = Extension.GetRootPropertyName(jsonContent);
                        McpServerItem item;
                        var server = JsonNode.Parse(jsonContent)?[name]?.AsObject();
                        if (server != null)
                        {
                            if (server.ContainsKey("command"))
                            {
                                item = new StdIOServerItem()
                                {
                                    Command = server["command"]?.ToString(),
                                    Argument = server["args"]?.AsArray()?.Select(a =>
                                    {
                                        if (a != null) return a.ToString();
                                        return string.Empty;
                                    }).Where(s => !string.IsNullOrEmpty(s.Trim())).ToList(),
                                };
                            }
                            else if (server.ContainsKey("url"))
                            {
                                item = new SseServerItem()
                                {
                                    Url = server["url"]?.ToString(),
                                };
                                if (server.ContainsKey("headers"))
                                {
                                    var o = server["headers"]?.AsObject();
                                    if (o != null)
                                    {
                                        var dictionary = new Dictionary<string, string>();
                                        foreach (var (key, value) in o)
                                        {
                                            if (value != null)
                                            {
                                                dictionary[key] = value.ToString();
                                            }
                                        }

                                        ((SseServerItem)item).AdditionalHeaders = dictionary;
                                    }
                                }
                            }
                            else
                            {
                                MessageEventBus.Publish("Unsupported MCP server format!");
                                continue;
                            }

                            item.Name = name;
                            Items.Add(item);
                            SelectedServerItem = item;
                            if (await item.RefreshToolsAsync())
                            {
                                MessageEventBus.Publish(
                                    $"已导入MCP服务器: {item.Name},工具数量: {item.AvailableTools?.Count ?? 0}");
                            }
                            else
                            {
                                MessageEventBus.Publish($"导入MCP服务器发生异常：{item.ErrorMessage}，请检查服务或配置是否正确！");
                            }
                        }
                        else
                        {
                            MessageEventBus.Publish("Invalid MCP server format!");
                        }
                    }
                    else
                    {
                        MessageEventBus.Publish("Invalid JSON format!");
                    }
                }
                catch (Exception ex)
                {
                    MessageEventBus.Publish($"Error importing MCP servers: {ex.Message}");
                }
            }
        });
        CopySelectedItemCommand = new ActionCommand((o =>
        {
            if (o is McpServerItem item && item.AvailableTools != null)
            {
                var stringBuilder = new StringBuilder();
                foreach (var aiFunction in item.AvailableTools)
                {
                    stringBuilder.Append("Name: ").AppendLine(aiFunction.Name);
                    stringBuilder.Append("Description: ").AppendLine(aiFunction.Description);
                    stringBuilder.Append("Parameters: ").AppendLine(aiFunction.JsonSchema.ToString());
                    if (aiFunction.ReturnJsonSchema != null)
                    {
                        stringBuilder.Append("Returns: ").AppendLine(aiFunction.ReturnJsonSchema.ToString());
                    }
                }

                CommonCommands.CopyCommand.Execute(item);
            }
        }));
        AddNewCommand = new ActionCommand((o =>
        {
            if (o is string type)
            {
                McpServerItem? item = type switch
                {
                    "stdio" => new StdIOServerItem(),
                    "sse" => new SseServerItem(),
                    _ => null
                };
                if (item != null)
                {
                    item.Name = "New Server";
                    Items.Add(item);
                    SelectedServerItem = item;
                }
            }
        }));
        ReloadCommand = new RelayCommand(async () =>
        {
            try
            {
                this.IsLoaded = false;
                this.IsInitialized = false;
                await this.EnsureAsync();
            }
            catch (Exception e)
            {
                MessageEventBus.Publish($"重载MCP服务器配置失败: {e.Message}");
            }
        });
        RefreshToolsCommand = new RelayCommand(async () =>
        {
            try
            {
                await InitializeToolsAsync();
                MessageEventBus.Publish("已刷新MCP服务器工具列表");
            }
            catch (Exception e)
            {
                MessageEventBus.Publish($"刷新MCP服务器工具列表失败: {e.Message}");
            }
        });
        SaveCommand = new RelayCommand(async () =>
        {
            foreach (var item in Items)
            {
                if (!item.Validate())
                {
                    MessageEventBus.Publish($"服务{item.Name}配置有误，请检查！");
                    return;
                }
            }

            if (Items.DistinctBy(item => item.Name).Count() != Items.Count)
            {
                MessageEventBus.Publish("服务名称不能重复，请检查！");
                return;
            }

            var builtinPluginNames = ServiceLocator.GetService<IBuiltInFunctionsCollection>()!
                .Select((group => group.Name))
                .ToArray();
            foreach (var item in Items)
            {
                if (builtinPluginNames.Contains(item.Name))
                {
                    MessageEventBus.Publish($"服务名称 {item.Name} 已被系统保留，请更换名称！");
                    return;
                }
            }

            var fullPath = Path.GetFullPath(FileName);
            await Items.SaveJsonToFileAsync(fullPath, Extension.DefaultJsonSerializerOptions);
            MessageEventBus.Publish("已保存MCP服务器配置");
        });
    }

    public async Task LoadAsync()
    {
        if (IsLoaded)
        {
            return;
        }

        var fileInfo = new FileInfo(Path.GetFullPath(FileName));
        if (!fileInfo.Exists)
        {
            return;
        }

        foreach (var mcpServerItem in Items)
        {
            await mcpServerItem.DisposeAsync();
        }

        await using (var fileStream = fileInfo.OpenRead())
        {
            var deserialize =
                JsonSerializer.Deserialize<IList<McpServerItem>>(fileStream,
                    Extension.DefaultJsonSerializerOptions);
            if (deserialize != null)
            {
                this.Items = new ObservableCollection<McpServerItem>(deserialize);
            }
        }

        this.IsLoaded = true;
        MessageEventBus.Publish("已加载MCP服务器配置");
    }

    public async Task EnsureAsync()
    {
        if (!this.IsLoaded)
        {
            await this.LoadAsync();
        }

        if (this.IsLoaded && !this.IsInitialized)
        {
            await this.InitializeToolsAsync();
        }
    }

    public IAIFunctionGroup TryGet(IAIFunctionGroup functionGroup)
    {
        //相同配置的mcp将使用唯一实例
        var hashCode = functionGroup.GetUniqueId();
        foreach (var item in this.EnabledServers)
        {
            if (item.GetUniqueId() == hashCode)
            {
                return item;
            }
        }

        return functionGroup;
    }

    public async Task InitializeToolsAsync()
    {
        if (this.IsInitialized)
        {
            return;
        }

        await Parallel.ForEachAsync(this.EnabledServers,
            async (mcpServerItem, ct) => { await mcpServerItem.RefreshToolsAsync(ct); });
        this.IsInitialized = true;
        MessageEventBus.Publish("已加载MCP服务器工具列表");
    }

    public async void DeleteServerItem(McpServerItem item)
    {
        Items.Remove(item);
        if (SelectedServerItem == item)
        {
            SelectedServerItem = null;
        }

        await item.DisposeAsync();
    }

    public IEnumerator<IAIFunctionGroup> GetEnumerator()
    {
        return EnabledServers.Cast<IAIFunctionGroup>().GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var item in _items)
        {
            await item.DisposeAsync();
        }
    }

    public IEnumerable<IAIFunctionGroup> GetFunctionGroups()
    {
        return this;
    }
}