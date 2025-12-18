using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using LLMClient.ToolCall;
using LLMClient.ToolCall.Servers;
using Microsoft.Extensions.AI;

namespace LLMClient.Abstraction;

/*注意：functiongroup和AIFunction本身不具有可enable特性，而是由包装器实现，这样可以即使共享单例也可以选择不同函数*/

/// <summary>
/// llm可调用函数组，具有自我描述特征；所以除了Function Lists之外还包括<see cref="AdditionPrompt"/>
/// </summary>
[JsonDerivedType(typeof(StdIOServerItem), "stdio")]
[JsonDerivedType(typeof(SseServerItem), "sse")]
[JsonDerivedType(typeof(FileSystemPlugin), "filesystemplugin")]
[JsonDerivedType(typeof(WinCLIPlugin), "winclipplugin")]
[JsonDerivedType(typeof(GoogleSearchPlugin), "googlesearchplugin")]
[JsonDerivedType(typeof(UrlFetcherPlugin), "urlfetcherplugin")]
public interface IAIFunctionGroup : ICloneable
{
    string Name { get; }

    string? AdditionPrompt { get; }

    /// <summary>
    /// 工具列表
    /// </summary>
    /// <returns></returns>
    IReadOnlyList<AIFunction>? AvailableTools { get; }

    bool IsAvailable { get; }

    string GetUniqueId();

    Task EnsureAsync(CancellationToken token);
}