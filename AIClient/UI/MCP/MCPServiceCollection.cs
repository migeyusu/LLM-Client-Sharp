using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using LLMClient.UI.Component;
using Microsoft.Extensions.AI;
using Microsoft.Win32;
using Microsoft.Xaml.Behaviors.Core;
using ModelContextProtocol.Client;

namespace LLMClient.UI.MCP;

public class McpServiceCollection : BaseViewModel
{
    public const string FileName = "mcp_servers.json";

    private McpServerItem? _selectedServerItem;
    public ObservableCollection<McpServerItem> Items { get; set; } = new ObservableCollection<McpServerItem>();

    public ICommand ImportFromJsonCommand => new RelayCommand(() =>
    {
        //只适配claude/jetbrains的mcp.json
        var jsonPreviewWindow = new JsonPreviewWindow()
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
                        Argument = server["args"]?.AsArray()?.Select(a => a.ToString()).ToList(),
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
        var fullPath = Path.GetFullPath(FileName);
        var json = JsonSerializer.Serialize(this.Items, new JsonSerializerOptions
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() }
        });
        File.WriteAllText(fullPath, json);
        MessageEventBus.Publish("已保存MCP服务器配置");
    });

    public McpServiceCollection()
    {
    }

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
        }
        catch (Exception e)
        {
            Trace.TraceError(e.ToString());
        }
    }

    public async Task InitializeAsync()
    {
        foreach (var mcpServerItem in this.Items)
        {
            try
            {
                mcpServerItem.Tools = (await mcpServerItem.ListOps()).ToArray();
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
}

[JsonDerivedType(typeof(StdIOServerItem), "stdio")]
[JsonDerivedType(typeof(SseServerItem), "sse")]
public abstract class McpServerItem : NotifyDataErrorInfoViewModelBase
{
    private string? _name;
    private IList<AITool>? _tools;
    private bool _isToolAvailable;

    [JsonIgnore] public abstract string Type { get; }

    public string? Name
    {
        get => _name;
        set
        {
            if (value == _name) return;
            _name = value;
            OnPropertyChanged();
            ClearError();
            if (string.IsNullOrEmpty(value))
            {
                AddError("Name cannot be null or empty.");
            }
        }
    }

    [JsonIgnore]
    public IList<AITool>? Tools
    {
        get => _tools;
        set
        {
            if (Equals(value, _tools)) return;
            _tools = value;
            OnPropertyChanged();
        }
    }

    [JsonIgnore]
    public bool IsToolAvailable
    {
        get => _isToolAvailable;
        set
        {
            if (value == _isToolAvailable) return;
            _isToolAvailable = value;
            OnPropertyChanged();
        }
    }

    public ICommand RefreshCommand => new RelayCommand(async () =>
    {
        try
        {
            this.Tools = (await this.ListOps()).ToArray();
        }
        catch (Exception ex)
        {
            MessageEventBus.Publish($"Error refreshing tools: {ex.Message}");
        }
    });

    public async Task<IEnumerable<AITool>> ListOps()
    {
        var transport = this.Create();
        var client = await McpClientFactory.CreateAsync(transport);
        return (await client.ListToolsAsync());
    }

    public abstract IClientTransport Create();

    public abstract bool Validate();
}

public class StdIOServerItem : McpServerItem
{
    private string? _command;
    private string? _workingDirectory;
    private IList<string>? _argument;
    private string? _environmentString;
    private IList<EnvironmentVariableItem>? _environmentVariable;
    public override string Type => "stdio";

    public override bool Validate()
    {
        if (string.IsNullOrEmpty(Command))
        {
            return false;
        }

        return true;
    }

    public string? Command
    {
        get => _command;
        set
        {
            if (value == _command) return;
            _command = value;
            OnPropertyChanged();
            ClearError();
            if (string.IsNullOrEmpty(value))
            {
                AddError("Command cannot be null or empty.");
            }
        }
    }

    public IList<string>? Argument
    {
        get => _argument;
        set
        {
            if (Equals(value, _argument)) return;
            _argument = value;
            OnPropertyChanged();
        }
    }

    [JsonIgnore]
    public string? EnvironmentString
    {
        get => _environmentString;
        set
        {
            if (value == _environmentString) return;
            _environmentString = value;
            OnPropertyChanged();
        }
    }

    public IList<EnvironmentVariableItem>? EnvironmentVariable
    {
        get => _environmentVariable;
        set
        {
            _environmentVariable = value;
            this.EnvironmentString = value != null
                ? string.Join(";", value.Select(item => $"{item.Name}={item.Value}"))
                : null;
        }
    }

    public string? WorkingDirectory
    {
        get => _workingDirectory;
        set
        {
            if (value == _workingDirectory) return;
            _workingDirectory = value;
            OnPropertyChanged();
        }
    }

    public ICommand SelectFolderCommand => new RelayCommand(() =>
    {
        var openFolderDialog = new OpenFolderDialog();
        if (openFolderDialog.ShowDialog() == true)
        {
            this.WorkingDirectory = openFolderDialog.FolderName;
        }
    });

    public ICommand SelectEnvironmentCommand => new RelayCommand(() =>
    {
        var envWindow = new EnvironmentVariablesWindow();
        if (envWindow.ShowDialog() == true)
        {
            if (envWindow.DataContext is EnvironmentVariablesViewModel viewModel)
            {
                if (viewModel.IsSystemVariablesIncluded)
                {
                    this.EnvironmentVariable = viewModel.SystemVariables != null
                        ? viewModel.SystemVariables.Concat(viewModel.UserVariables).ToArray()
                        : viewModel.UserVariables;
                }
                else
                {
                    this.EnvironmentVariable = viewModel.UserVariables;
                }
            }
        }
    });

    public override IClientTransport Create()
    {
        if (string.IsNullOrEmpty(Command))
        {
            throw new NotSupportedException("Command cannot be null or empty.");
        }

        var options = new StdioClientTransportOptions()
        {
            Name = this.Name,
            WorkingDirectory = this.WorkingDirectory,
            Command = this.Command,
            Arguments = this.Argument,
            EnvironmentVariables = this.EnvironmentVariable?
                .ToDictionary(kvp => kvp.Name!, kvp => kvp.Value)
        };
        return new StdioClientTransport(options);
    }
}

public class SseServerItem : McpServerItem
{
    private string? _url;
    public override string Type => "sse";

    public override bool Validate()
    {
        if (string.IsNullOrEmpty(this.Url))
        {
            return false;
        }

        return true;
    }

    public string? Url
    {
        get => _url;
        set
        {
            if (value == _url) return;
            _url = value;
            OnPropertyChanged();
            ClearError();
            if (string.IsNullOrEmpty(value))
            {
                AddError("Url cannot be null or empty.");
            }
        }
    }

    public override IClientTransport Create()
    {
        if (string.IsNullOrEmpty(this.Url))
        {
            throw new NotSupportedException("Url cannot be null or empty.");
        }

        var sseClientTransportOptions = new SseClientTransportOptions()
        {
            Name = this.Name,
            Endpoint = new Uri(this.Url),
            TransportMode = HttpTransportMode.AutoDetect
        };
        return new SseClientTransport(sseClientTransportOptions);
    }
}