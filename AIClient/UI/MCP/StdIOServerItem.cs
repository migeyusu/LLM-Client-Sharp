using System.Text.Json.Serialization;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using Microsoft.SemanticKernel;
using Microsoft.Win32;
using ModelContextProtocol.Client;

namespace LLMClient.UI.MCP;

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

    public override string GetUniqueId()
    {
        return $"stdio:{Name},{Command},{WorkingDirectory},{string.Join(",", Argument ?? new List<string>())}" +
               $",{string.Join(";", EnvironmentVariable?.Select(ev => $"{ev.Name}={ev.Value}") ?? new List<string>())}";
    }

    protected override IClientTransport GetTransport()
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