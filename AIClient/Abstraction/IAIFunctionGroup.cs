using System.Text.Json.Serialization;
using LLMClient.UI.MCP;
using LLMClient.UI.MCP.Servers;
using Microsoft.Extensions.AI;

namespace LLMClient.Abstraction;

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

    bool IsToolAvailable { get; }

    string GetUniqueId();

    Task EnsureAsync(CancellationToken token);
}