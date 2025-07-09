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

    /// <summary>
    /// 工具列表
    /// </summary>
    /// <returns></returns>
    IReadOnlyList<AIFunction>? AvailableTools { get; }

    bool IsToolAvailable { get; }

    string GetUniqueId();

    Task EnsureAsync(CancellationToken token);
}

public class AIFunctionGroupComparer : IEqualityComparer<IAIFunctionGroup>
{
    public static AIFunctionGroupComparer Instance => new AIFunctionGroupComparer();

    public bool Equals(IAIFunctionGroup? x, IAIFunctionGroup? y)
    {
        if (ReferenceEquals(x, y)) return true;
        if (x is null) return false;
        if (y is null) return false;
        if (x.GetType() != y.GetType()) return false;
        return x.GetUniqueId() == y.GetUniqueId();
    }

    public int GetHashCode(IAIFunctionGroup obj)
    {
        // return obj.GetUniqueId().GetHashCode();
        return HashCode.Combine(obj.GetUniqueId());
    }
}