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
public interface IAIFunctionGroup : ICloneable
{
    string Name { get; }

    bool IsEnabled { get; }

    /// <summary>
    /// 获取工具列表
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<IList<AIFunction>> GetToolsAsync(CancellationToken cancellationToken = default);
    
    string GetUniqueId();
}