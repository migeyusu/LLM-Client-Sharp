using System.Collections.Concurrent;
using Trace = System.Diagnostics.Trace;

namespace LLMClient.ContextEngineering.Analysis;

public class SymbolIndexService
{
    // 主键索引：SymbolKey -> Entry
    private readonly ConcurrentDictionary<string, SymbolInfo> _keyIndex = new();

    // 倒排索引：Name -> List<SymbolKey> (支持重载)
    private readonly ConcurrentDictionary<string, List<string>> _nameIndex = new();

    public void AddSymbol(SymbolInfo info)
    {
        try
        {
            var keyStr = info.SymbolId;
            // 防止重复索引
            if (!_keyIndex.TryAdd(keyStr, info)) return;

            // 更新名称索引
            _nameIndex.AddOrUpdate(
                info.Name,
                [keyStr],
                (_, list) =>
                {
                    list.Add(keyStr);
                    return list;
                }
            );
        }
        catch (Exception ex)
        {
            // Log error
            Trace.TraceError($"Failed to add symbol {info.Name} ({info.SymbolId}): {ex}");
        }
    }

    public SymbolInfo? GetByKey(string symbolKey)
    {
        _keyIndex.TryGetValue(symbolKey, out var entry);
        return entry;
    }

    public void InvalidateByFile(string filePath)
    {
        var keysToRemove = _keyIndex.Values
            .Where(e => e.FilesPath.Contains(filePath))
            .Select(e => e.SymbolId)
            .ToList();

        foreach (var key in keysToRemove)
        {
            if (_keyIndex.TryRemove(key, out var removed))
            {
                // 同步更新名称索引
                if (_nameIndex.TryGetValue(removed.Name, out var list))
                {
                    list.Remove(key);
                    if (list.Count == 0) _nameIndex.TryRemove(removed.Name, out _);
                }
            }
        }
    }

    public List<(SymbolInfo sym, double score)> Search(string query,
        string? kind = null,
        string? scope = null)
    {
        var scored = new List<(SymbolInfo sym, double score)>();
        foreach (var sym in _keyIndex.Values)
        {
            // kind 过滤（大小写不敏感）
            if (!string.IsNullOrWhiteSpace(kind) &&
                !sym.Kind.Equals(kind, StringComparison.OrdinalIgnoreCase))
                continue;

            // scope 过滤：匹配文件路径片段 或 签名前缀（命名空间）
            if (!string.IsNullOrWhiteSpace(scope) &&
                !sym.FilesPath.Any(fp => fp.Contains(scope, StringComparison.OrdinalIgnoreCase)) &&
                !sym.Signature.Contains(scope, StringComparison.OrdinalIgnoreCase))
                continue;

            var score = ScoreSymbol(sym, query);
            if (score > 0)
                scored.Add((sym, score));
        }

        return scored;
    }

    private static double ScoreSymbol(SymbolInfo sym, string query)
    {
        // 精确名称匹配权重最高，向下依次降级
        if (sym.Name.Equals(query, StringComparison.OrdinalIgnoreCase)) return 1.0;
        if (sym.Name.StartsWith(query, StringComparison.OrdinalIgnoreCase)) return 0.85;
        if (sym.Name.Contains(query, StringComparison.OrdinalIgnoreCase)) return 0.70;
        if (sym.Signature.Contains(query, StringComparison.OrdinalIgnoreCase)) return 0.45;

        var summary = sym.Summary;
        if (summary?.Contains(query, StringComparison.OrdinalIgnoreCase) == true) return 0.30;

        return 0;
    }

    /// <summary>
    /// 获取所有已索引的符号（用于需要遍历全集的场景，如特性查找）
    /// </summary>
    public IEnumerable<SymbolInfo> GetAllSymbols()
    {
        return _keyIndex.Values;
    }

    /// <summary>
    /// 按文件路径和行号范围查找符号（用于代码位置反向定位）
    /// </summary>
    public SymbolInfo? FindByLocation(string filePath, int lineNumber)
    {
        return _keyIndex.Values.FirstOrDefault(sym =>
            sym.Locations.Any(loc =>
                string.Equals(loc.FilePath, filePath, StringComparison.OrdinalIgnoreCase) &&
                lineNumber >= loc.Location.Start.Line &&
                lineNumber <= loc.Location.End.Line));
    }

    public void Clear()
    {
        _keyIndex.Clear();
        _nameIndex.Clear();
    }
}