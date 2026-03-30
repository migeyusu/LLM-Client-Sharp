using System.Text;
using System.Text.Json.Serialization;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using LLMClient.Component.UserControls;
using Microsoft.Win32;
using ModelContextProtocol.Client;

namespace LLMClient.ToolCall.MCP;

public class StdIOServerItem : McpServerItem
{
    private const string PythonBootstrapFileName = "mcp_python_stdio_bootstrap.py";
    private const string PythonScriptBootstrap = """
import builtins
import os
import sys

_original_print = builtins.print

def _mcp_safe_print(*args, **kwargs):
    if 'file' not in kwargs or kwargs['file'] is None:
        kwargs['file'] = sys.stderr
    return _original_print(*args, **kwargs)

builtins.print = _mcp_safe_print
script_path = os.path.abspath(sys.argv[1])
script_args = sys.argv[2:]
script_dir = os.path.dirname(script_path)
if script_dir:
    sys.path.insert(0, script_dir)
sys.argv = [script_path, *script_args]
main_globals = globals()
main_globals['__file__'] = script_path
main_globals['__package__'] = None
main_globals['__cached__'] = None
with open(script_path, 'rb') as script_file:
    exec(compile(script_file.read(), script_path, 'exec'), main_globals)
""";

    private static readonly Lock PythonBootstrapLock = new();

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
        get;
        set
        {
            if (value == field) return;
            field = value;
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
        get;
        set
        {
            if (Equals(value, field)) return;
            field = value;
            OnPropertyChanged();
        }
    }

    [JsonIgnore]
    public string? EnvironmentString
    {
        get;
        set
        {
            if (value == field) return;
            field = value;
            OnPropertyChanged();
        }
    }

    public IList<VariableItem>? EnvironmentVariable
    {
        get;
        set
        {
            field = value;
            this.EnvironmentString = value != null
                ? string.Join(";", value.Select(item => $"{item.Name}={item.Value}"))
                : null;
        }
    }

    public string? WorkingDirectory
    {
        get;
        set
        {
            if (value == field) return;
            field = value;
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
        if (envWindow.DataContext is EnvironmentVariablesViewModel viewModel)
        {
            var environmentVariable = this.EnvironmentVariable;
            if (environmentVariable != null)
            {
                foreach (var variableItem in environmentVariable)
                {
                    viewModel.UserVariables.Add(variableItem);
                }
            }

            if (envWindow.ShowDialog() == true)
            {
                this.EnvironmentVariable = viewModel.UserVariables.ToArray();
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
        return new StdioClientTransport(CreateTransportOptions());
    }

    internal StdioClientTransportOptions CreateTransportOptions()
    {
        if (string.IsNullOrEmpty(Command))
        {
            throw new NotSupportedException("Command cannot be null or empty.");
        }

        var command = NormalizeToken(this.Command)
                      ?? throw new NotSupportedException("Command cannot be null or empty.");
        var arguments = this.Argument?
            .Select(NormalizeToken)
            .Where(arg => !string.IsNullOrWhiteSpace(arg))
            .Cast<string>()
            .ToList();
        var workingDirectory = NormalizeToken(this.WorkingDirectory);
        var environmentVariables = BuildEnvironmentVariables(this.EnvironmentVariable);

        if (TryWrapPythonScriptCommand(ref command, arguments, ref workingDirectory, ref environmentVariables,
                out var wrappedArguments))
        {
            arguments = wrappedArguments;
        }

        var options = new StdioClientTransportOptions()
        {
            Name = this.Name,
            WorkingDirectory = workingDirectory,
            Command = command,
            Arguments = arguments,
            EnvironmentVariables = environmentVariables
        };
        return options;
    }

    private static Dictionary<string, string?>? BuildEnvironmentVariables(IEnumerable<VariableItem>? variables)
    {
        if (variables == null)
        {
            return null;
        }

        var dictionary = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var variable in variables)
        {
            var name = NormalizeToken(variable.Name);
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            dictionary[name] = variable.Value ?? string.Empty;
        }

        return dictionary.Count > 0 ? dictionary : null;
    }

    private static bool TryWrapPythonScriptCommand(
        ref string command,
        IList<string>? arguments,
        ref string? workingDirectory,
        ref Dictionary<string, string?>? environmentVariables,
        out List<string> wrappedArguments)
    {
        wrappedArguments = arguments?.ToList() ?? new List<string>();
        if (!IsPythonCommand(command) || wrappedArguments.Count == 0)
        {
            return false;
        }

        var scriptPath = NormalizeToken(wrappedArguments[0]);
        if (string.IsNullOrWhiteSpace(scriptPath) || !scriptPath.EndsWith(".py", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(workingDirectory))
        {
            var scriptDirectory = Path.GetDirectoryName(scriptPath);
            if (!string.IsNullOrWhiteSpace(scriptDirectory))
            {
                workingDirectory = scriptDirectory;
            }
        }

        environmentVariables ??= new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        environmentVariables["MCP_TRANSPORT"] = "stdio";
        environmentVariables["PYTHONUNBUFFERED"] = "1";

        var bootstrapPath = EnsurePythonBootstrapFile();

        if (OperatingSystem.IsWindows())
        {
            wrappedArguments = new List<string>
            {
                "-NoProfile",
                "-Command",
                BuildPowerShellPythonCommand(command, bootstrapPath, scriptPath, arguments!.Skip(1))
            };
            command = "powershell.exe";
            return true;
        }

        wrappedArguments = new List<string>
        {
            "-u",
            bootstrapPath,
            scriptPath
        };
        for (var i = 1; i < arguments!.Count; i++)
        {
            wrappedArguments.Add(arguments[i]);
        }

        return true;
    }

    private static bool IsPythonCommand(string command)
    {
        var fileName = Path.GetFileName(command);
        return fileName.Equals("python", StringComparison.OrdinalIgnoreCase)
               || fileName.Equals("python.exe", StringComparison.OrdinalIgnoreCase)
               || fileName.Equals("python3", StringComparison.OrdinalIgnoreCase)
               || fileName.Equals("python3.exe", StringComparison.OrdinalIgnoreCase)
               || fileName.Equals("py", StringComparison.OrdinalIgnoreCase)
               || fileName.Equals("py.exe", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildPowerShellPythonCommand(
        string pythonCommand,
        string bootstrapPath,
        string scriptPath,
        IEnumerable<string> extraArguments)
    {
        var segments = new List<string>
        {
            "&",
            ToPowerShellSingleQuotedLiteral(pythonCommand),
            ToPowerShellSingleQuotedLiteral(bootstrapPath),
            ToPowerShellSingleQuotedLiteral(scriptPath)
        };
        segments.AddRange(extraArguments.Select(ToPowerShellSingleQuotedLiteral));
        return string.Join(" ", segments);
    }

    private static string ToPowerShellSingleQuotedLiteral(string value)
    {
        return $"'{value.Replace("'", "''")}'";
    }

    private static string EnsurePythonBootstrapFile()
    {
        var bootstrapDirectory = Extension.TempPath;
        Directory.CreateDirectory(bootstrapDirectory);
        var bootstrapPath = Path.Combine(bootstrapDirectory, PythonBootstrapFileName);

        lock (PythonBootstrapLock)
        {
            if (!File.Exists(bootstrapPath) || !string.Equals(File.ReadAllText(bootstrapPath), PythonScriptBootstrap, StringComparison.Ordinal))
            {
                File.WriteAllText(bootstrapPath, PythonScriptBootstrap, new UTF8Encoding(false));
            }
        }

        return bootstrapPath;
    }

    private static string? NormalizeToken(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        if (trimmed.Length >= 2 &&
            ((trimmed[0] == '"' && trimmed[^1] == '"') || (trimmed[0] == '\'' && trimmed[^1] == '\'')))
        {
            trimmed = trimmed[1..^1].Trim();
        }

        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }
}