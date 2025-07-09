using System.Collections;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using LLMClient.Abstraction;
using LLMClient.UI.Component;
using Microsoft.Extensions.AI;
using Microsoft.Xaml.Behaviors.Core;

namespace LLMClient.UI.MCP;

/// <summary>
/// 经过慎重考虑，设置为单例，因为mcp服务可能由客户端命令启动，重复启动会清空实例的上下文状态
/// </summary>
public class McpServiceCollection : BaseViewModel, IMcpServiceCollection, IAsyncDisposable
{
    public const string FileName = "mcp_servers.json";

    private McpServerItem? _selectedServerItem;
    private ObservableCollection<McpServerItem> _items = new ObservableCollection<McpServerItem>();
    private bool _isInitialized;
    private bool _isLoaded;

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

    public ICommand ImportFromJsonCommand => new RelayCommand(async () =>
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
                        }
                        else
                        {
                            MessageEventBus.Publish("Unsupported MCP server format!");
                            continue;
                        }

                        item.Name = name;
                        Items.Add(item);
                        SelectedServerItem = item;
                        if (await item.ListToolsAsync())
                        {
                            MessageEventBus.Publish($"已导入MCP服务器: {item.Name},工具数量: {item.AvailableTools?.Count ?? 0}");
                            return;
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

    public ICommand AddNewCommand => new ActionCommand((o =>
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

    public ICommand SaveCommand => new RelayCommand(() =>
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

        var fullPath = Path.GetFullPath(FileName);
        var json = JsonSerializer.Serialize(this.Items, new JsonSerializerOptions
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() }
        });
        File.WriteAllText(fullPath, json);
        MessageEventBus.Publish("已保存MCP服务器配置");
    });

    public ICommand RefreshToolsCommand => new RelayCommand(async () =>
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

    public ICommand ReloadCommand => new RelayCommand(async () =>
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

        try
        {
            foreach (var mcpServerItem in Items)
            {
                await mcpServerItem.DisposeAsync();
            }

            await using (var fileStream = fileInfo.OpenRead())
            {
                var deserialize = JsonSerializer.Deserialize<IList<McpServerItem>>(fileStream);
                if (deserialize != null)
                {
                    this.Items = new ObservableCollection<McpServerItem>(deserialize);
                }
            }

            this.IsLoaded = true;
            MessageEventBus.Publish("已加载MCP服务器配置");
        }
        catch (Exception e)
        {
            Trace.TraceError(e.ToString());
        }
    }

    public async Task EnsureAsync()
    {
        if (!this.IsLoaded)
        {
            await this.LoadAsync();
        }

        if (!this.IsInitialized)
        {
            await this.InitializeToolsAsync();
        }
    }

    public IAIFunctionGroup TryGet(IAIFunctionGroup functionGroup)
    {
        //相同配置的mcp将使用唯一实例
        var hashCode = functionGroup.GetUniqueId();
        foreach (var item in this.Items)
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

        foreach (var mcpServerItem in this.Items)
        {
            try
            {
                await mcpServerItem.ListToolsAsync();
            }
            catch (Exception e)
            {
                Trace.TraceWarning($"mcp service {mcpServerItem.Name} get tools failed: {e.Message}");
            }
        }

        this.IsInitialized = true;
        MessageEventBus.Publish("已刷新MCP服务器工具列表");
    }

    public async void DeleteServerItem(McpServerItem? item)
    {
        if (item == null) return;
        Items.Remove(item);
        if (SelectedServerItem == item)
        {
            SelectedServerItem = null;
        }

        await item.DisposeAsync();
    }

    public IEnumerator<IAIFunctionGroup> GetEnumerator()
    {
        return Items.Cast<IAIFunctionGroup>().GetEnumerator();
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
}