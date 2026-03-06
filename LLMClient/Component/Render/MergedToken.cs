namespace LLMClient.Component.Render;

/// <summary>
/// 合并后的 Token 数据，仅含纯数据字段，不依赖任何 WPF 类型。
/// 由后台线程生成，传递给 UI 线程批量创建 TextmateColoredRun。
/// </summary>
internal sealed class MergedToken
{
    /// <summary>合并后的文本（相邻 scopesKey 相同的 token 文本拼接）</summary>
    public string Text { get; }

    /// <summary>原始 scopes 数组（保留首个 token 的 scopes，相同 key 的 scopes 必然相同）</summary>
    public List<string> Scopes { get; }

    /// <summary>由 scopes 拼接的稳定 key，供 ThemeMatchCache 查询</summary>
    public string ScopesKey { get; }

    public MergedToken(string text, List<string> scopes, string scopesKey)
    {
        Text = text;
        Scopes = scopes;
        ScopesKey = scopesKey;
    }
}