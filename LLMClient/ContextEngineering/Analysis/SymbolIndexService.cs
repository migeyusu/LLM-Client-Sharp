using System.Collections.Concurrent;

namespace LLMClient.ContextEngineering.Analysis;

internal class SymbolIndexService
{
    // 主键索引：SymbolKey -> Entry
    private readonly ConcurrentDictionary<string, SymbolInfo> _keyIndex = new();

    // 倒排索引：Name -> List<SymbolKey> (支持重载)
    private readonly ConcurrentDictionary<string, List<string>> _nameIndex = new();

    public void AddSymbol(SymbolInfo info)
    {
        try
        {
            var keyStr = info.UniqueId ?? info.Signature;

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
            .Select(e => e.UniqueId ?? e.Signature)
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

    public List<SymbolInfo> Search(string query, int topN = 10)
    {
        // 极简搜索：名称完全匹配 > 名称包含 > 签名包含
        // 实际生产可用 Lucene，这里保持轻量
        return _keyIndex.Values
            .Where(x => x.Name.Contains(query) || x.Signature.Contains(query))
            .Take(topN)
            .ToList();
    }

    public void Clear()
    {
        _keyIndex.Clear();
        _nameIndex.Clear();
    }
}