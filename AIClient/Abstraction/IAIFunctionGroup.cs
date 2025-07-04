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
    string Name { get; }

    bool IsEnabled { get; }

    /// <summary>
    /// 获取工具列表
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<IList<AIFunction>> GetToolsAsync(CancellationToken cancellationToken = default);
}