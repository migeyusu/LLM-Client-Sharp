namespace LLMClient.ContextEngineering.Tools.Models;

/// <summary>
/// 符号基础视图，包含最基本的标识信息
/// </summary>
public abstract record SymbolViewBase
{
    /// <summary>
    /// 全局唯一标识符 (SymbolId/TopicId/Key)
    /// </summary>
    public required string SymbolId { get; init; }
    
    /// <summary>
    /// 短名称
    /// </summary>
    public required string Name { get; init; }
    
    /// <summary>
    /// 完整签名或显示名称
    /// </summary>
    public string Signature { get; init; } = "";
}

/// <summary>
/// 包含描述信息的符号视图（类型、成员等）
/// </summary>
public abstract record DescribedSymbolViewBase : SymbolViewBase
{
    /// <summary>
    /// 符号类型 (Class, Method, Property, etc.)
    /// </summary>
    public required string Kind { get; init; }
    
    /// <summary>
    /// 可访问性 (Public, Private, etc.)
    /// </summary>
    public string Accessibility { get; init; } = "";
    
    /// <summary>
    /// XML 文档摘要或说明
    /// </summary>
    public string? Summary { get; init; }
}

/// <summary>
/// 包含来源信息的符号视图
/// </summary>
public abstract record SourcedSymbolViewBase : SymbolViewBase
{
    /// <summary>
    /// 来源 ("Roslyn" | "Index")
    /// </summary>
    public string Source { get; init; } = "";
}

/// <summary>
/// 包含多个位置信息的描述符号视图
/// </summary>
public abstract record MultiLocatableDescribedSymbolViewBase : DescribedSymbolViewBase
{
    public List<LocationView> Locations { get; init; } = [];
}
