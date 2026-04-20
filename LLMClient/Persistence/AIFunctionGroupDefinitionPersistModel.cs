using System.Text.Json.Serialization;
using LLMClient.Configuration;
using LLMClient.ToolCall;
using ModelContextProtocol.Client;

namespace LLMClient.Persistence;

[JsonDerivedType(typeof(StdIOServerItemPersistModel), "stdio")]
[JsonDerivedType(typeof(SseServerItemPersistModel), "sse")]
[JsonDerivedType(typeof(FileSystemPluginPersistModel), "filesystemplugin")]
[JsonDerivedType(typeof(WslCliPluginPersistModel), "wslcliplugin")]
[JsonDerivedType(typeof(WinCliPluginPersistModel), "wincliplugin")]
[JsonDerivedType(typeof(GoogleSearchPluginPersistModel), "googlesearchplugin")]
[JsonDerivedType(typeof(UrlFetcherPluginPersistModel), "urlfetcherplugin")]
[JsonDerivedType(typeof(ProjectAwarenessPluginPersistModel), "projectawarenessplugin")]
[JsonDerivedType(typeof(SymbolSemanticPluginPersistModel), "symbolsemanticplugin")]
[JsonDerivedType(typeof(CodeSearchPluginPersistModel), "codesearchplugin")]
[JsonDerivedType(typeof(CodeReadingPluginPersistModel), "codereadingplugin")]
[JsonDerivedType(typeof(CodeMutationPluginPersistModel), "codemutationplugin")]
public abstract class AIFunctionGroupDefinitionPersistModel;

public abstract class McpServerItemPersistModel : AIFunctionGroupDefinitionPersistModel
{
    public string Name { get; set; } = string.Empty;

    public Uri? ProjectUrl { get; set; }

    public string? UserPrompt { get; set; }

    public bool IsEnabled { get; set; } = true;
}

public sealed class StdIOServerItemPersistModel : McpServerItemPersistModel
{
    public string? Command { get; set; }

    public IList<string>? Argument { get; set; }

    public string? WorkingDirectory { get; set; }

    public IList<VariableItem>? EnvironmentVariable { get; set; }
}

public sealed class SseServerItemPersistModel : McpServerItemPersistModel
{
    public string? Url { get; set; }

    public HttpTransportMode TransportMode { get; set; } = HttpTransportMode.AutoDetect;

    public bool BufferedRequest { get; set; }

    public bool RemoveCharSet { get; set; }

    public IDictionary<string, string>? AdditionalHeaders { get; set; }

    public ProxySetting ProxySetting { get; set; } = new();
}

public sealed class FileSystemPluginPersistModel : AIFunctionGroupDefinitionPersistModel
{
    public string[]? BypassPaths { get; set; }
}

public sealed class WslCliPluginPersistModel : AIFunctionGroupDefinitionPersistModel
{
    public string[]? VerifyRequiredCommands { get; set; }

    public string WslDistributionName { get; set; } = string.Empty;

    public string WslUserName { get; set; } = string.Empty;

    public int TimeoutSeconds { get; set; } = 30;

    public bool MapWorkingDirectoryToWsl { get; set; } = true;
}

public sealed class WinCliPluginPersistModel : AIFunctionGroupDefinitionPersistModel
{
    public string[]? VerifyRequiredCommands { get; set; }
}

public sealed class GoogleSearchPluginPersistModel : AIFunctionGroupDefinitionPersistModel;

public sealed class UrlFetcherPluginPersistModel : AIFunctionGroupDefinitionPersistModel;

public sealed class ProjectAwarenessPluginPersistModel : AIFunctionGroupDefinitionPersistModel;

public sealed class SymbolSemanticPluginPersistModel : AIFunctionGroupDefinitionPersistModel;

public sealed class CodeSearchPluginPersistModel : AIFunctionGroupDefinitionPersistModel;

public sealed class CodeReadingPluginPersistModel : AIFunctionGroupDefinitionPersistModel;

public sealed class CodeMutationPluginPersistModel : AIFunctionGroupDefinitionPersistModel;

