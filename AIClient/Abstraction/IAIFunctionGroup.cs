using System.Text.Json.Serialization;
using LLMClient.UI.MCP;
using LLMClient.UI.MCP.Servers;
using Microsoft.Extensions.AI;

namespace LLMClient.Abstraction;

/*注意：functiongroup和AIFunction本身不具有可enable特性，而是由包装器实现，这样可以即使共享单例也可以选择不同函数*/

/// <summary>
/// 一组函数的接口定义
/// </summary>
[JsonDerivedType(typeof(StdIOServerItem), "stdio")]
[JsonDerivedType(typeof(SseServerItem), "sse")]
[JsonDerivedType(typeof(FileSystemPlugin), "filesystemplugin")]
[JsonDerivedType(typeof(WinCLIPlugin), "winclipplugin")]
[JsonDerivedType(typeof(GoogleSearchPlugin), "googlesearchplugin")]
public interface IAIFunctionGroup : ICloneable
{
    string Name { get; }

    string? AdditionPrompt { get; }

    /// <summary>
    /// 工具列表
    /// </summary>
    /// <returns></returns>
    IReadOnlyList<AIFunction>? AvailableTools { get; }

    string GetUniqueId();

    Task EnsureAsync(CancellationToken token);
}