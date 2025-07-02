using System.Text.Json.Serialization;
using LLMClient.UI.MCP;
using Microsoft.Extensions.AI;

namespace LLMClient.Abstraction;

/// <summary>
/// 一组函数的接口定义
/// </summary>
[JsonDerivedType(typeof(StdIOServerItem), "stdio")]
[JsonDerivedType(typeof(SseServerItem), "sse")]
public interface IAIFunctionGroup
{
    /// <summary>
    /// 获取可用的函数列表
    /// </summary>
    /// <returns></returns>
    IList<AIFunction>? AvailableTools { get; }

    string Name { get; }

    bool IsToolAvailable { get; }

    bool IsEnabled { get; }

    Task<IList<AIFunction>> SearchToolsAsync(CancellationToken cancellationToken = default);
}