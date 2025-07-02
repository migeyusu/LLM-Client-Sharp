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

public class McpServiceCollection : BaseViewModel, IMcpServiceCollection
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
            OnPropertyChanged(nameof(AvailableTools));
            OnPropertyChanged(nameof(ImportFromJsonCommand));
            OnPropertyChanged(nameof(AddNewCommand));
            OnPropertyChanged(nameof(SaveCommand));
        }
    }

    public IEnumerable<AITool> AvailableTools
    {
        get
        {
            foreach (var item in Items)
            {
                if (item.IsToolAvailable && item.IsEnabled)
                {
                    var functions = item.AvailableTools;
                    if (functions == null)
                    {
                        continue;
                    }

                    foreach (var itemTool in functions)
                    {
                        yield return itemTool;
                    }
                }
            }

            yield break;
        }
    }

    public bool IsInitialized
    {
        get => _isInitialized;
        set
        {
            if (value == _isInitialized) return;
            _isInitialized = value;
            OnPropertyChanged();
        }
    }

    public ICommand ImportFromJsonCommand => new RelayCommand(() =>
    {
        //只适配claude/jetbrains的mcp.json
        var jsonPreviewWindow = new JsonEditorWindow()
        {
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            JsonContent =
                "{\n \"code-analysis\": {\n      \"command\": \"uv\",\n      \"args\": [\n        \"--directory\",\n        \"/PATH/TO/YOUR/REPO\",\n        \"run\",\n        \"code_analysis.py\"\n      ]\n    } \n}"
        };
        if (jsonPreviewWindow.ShowDialog() == true)
        {
            try
            {
                var json = jsonPreviewWindow.JsonContent;
                var jsonDocument = JsonNode.Parse(json);
                if (jsonDocument == null)
                {
                    MessageEventBus.Publish("Invalid JSON format!");
                    return;
                }

                var name = Extension.GetRootPropertyName(json);
                McpServerItem item;
                var server = JsonNode.Parse(json)?[name]?.AsObject();
                if (server == null)
                {
                    MessageEventBus.Publish("Invalid MCP server format!");
                    return;
                }

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
                    return;
                }

                item.Name = name;
                Items.Add(item);
                SelectedServerItem = item;
                item.RefreshCommand.Execute(null);
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

        if (Items.DistinctBy((item => item.Name)).Count() != Items.Count)
        {
            MessageEventBus.Publish("服务名称不能重复，请检查！");
            return;
        }

        if (Items.Any((item => !item.IsToolAvailable)))
        {
            MessageEventBus.Publish("有服务无法连接，请检查！");
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
            await RefreshToolsAsync();
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
            await LoadAsync();
            MessageEventBus.Publish("已重新加载MCP服务器配置");
            await RefreshToolsAsync();
            MessageEventBus.Publish("已刷新MCP服务器工具列表");
        }
        catch (Exception e)
        {
            MessageEventBus.Publish($"重载MCP服务器配置失败: {e.Message}");
        }
    });

    public async Task LoadAsync()
    {
        var fileInfo = new FileInfo(Path.GetFullPath(FileName));
        if (!fileInfo.Exists)
        {
            return;
        }

        try
        {
            await using (var fileStream = fileInfo.OpenRead())
            {
                var deserialize = JsonSerializer.Deserialize<IList<McpServerItem>>(fileStream);
                if (deserialize != null)
                {
                    this.Items = new ObservableCollection<McpServerItem>(deserialize);
                }
            }

            this.IsInitialized = true;
        }
        catch (Exception e)
        {
            Trace.TraceError(e.ToString());
        }
    }

    public async Task RefreshToolsAsync()
    {
        foreach (var mcpServerItem in this.Items)
        {
            try
            {
                if (mcpServerItem.Validate())
                {
                    await mcpServerItem.RefreshOps();
                }
            }
            catch (Exception e)
            {
                Trace.TraceWarning($"mcp service {mcpServerItem.Name} get tools failed: {e.Message}");
            }
        }
    }

    public void DeleteServerItem(McpServerItem? item)
    {
        if (item == null) return;
        Items.Remove(item);
        if (SelectedServerItem == item)
        {
            SelectedServerItem = null;
        }
    }

    public IEnumerator<IAIFunctionGroup> GetEnumerator()
    {
        return Items.Cast<IAIFunctionGroup>().GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}