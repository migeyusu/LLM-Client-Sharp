using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text.Json;
using MimeTypes;

namespace LLMClient.Data;

public class HttpContentCache
{
    private static readonly string CacheFolderRelative =
        Extension.CacheFolderName + Path.DirectorySeparatorChar + nameof(HttpContentCache);

    const string CacheConfigFileName = "cache_index.json";
    private static string CacheFolderPath => Path.GetFullPath(CacheFolderRelative);

    private readonly string _cacheDirectory;

    private readonly string _indexPath; // 持久化索引文件的路径

    private readonly ConcurrentDictionary<string, AsyncLazyFile> _cache;

    public HttpContentCache(string cacheDirectory)
    {
        if (!Directory.Exists(cacheDirectory))
        {
            Directory.CreateDirectory(cacheDirectory);
        }

        _cacheDirectory = cacheDirectory;
        // 默认索引文件名为 index.json，存放在缓存目录下
        _indexPath = Path.Combine(_cacheDirectory, CacheConfigFileName);
        _cache = new ConcurrentDictionary<string, AsyncLazyFile>();
        LoadIndex(_indexPath);
    }

    /// <summary>
    /// 从 JSON 索引文件加载已有的缓存记录。
    /// </summary>
    private void LoadIndex(string indexPath)
    {
        if (!File.Exists(indexPath))
        {
            Trace.WriteLine("[Cache] Index file not found. Starting with an empty cache.");
            return;
        }

        try
        {
            var json = File.ReadAllText(indexPath);
            var options = Extension.DefaultJsonSerializerOptions;
            var persistedCache = JsonSerializer.Deserialize<Dictionary<string, string>>(json, options);
            if (persistedCache != null)
            {
                foreach (var item in persistedCache)
                {
                    var preCompletedLoader = new AsyncLazyFile(() => Task.FromResult(item.Value));
                    _cache.TryAdd(item.Key, preCompletedLoader);
                }

                Trace.WriteLine($"[Cache] Loaded {persistedCache.Count} items from index.");
            }
        }
        catch (Exception ex)
        {
            Trace.TraceError($"[Cache] Error loading cache index: {ex.Message}. Starting with an empty cache.");
            _cache.Clear();
        }
    }

    /// <summary>
    /// 将当前有效的缓存条目持久化到 JSON 索引文件。
    /// </summary>
    public async Task PersistIndexAsync()
    {
        var dictionaryToPersist = new Dictionary<string, string>();
        // 遍历缓存，只保存那些已经成功完成下载的条目
        foreach (var kvp in _cache)
        {
            var loader = kvp.Value;
            if (loader.Value.IsCompletedSuccessfully)
            {
                var fileName = await loader.Value;
                var path = Path.GetFullPath(fileName, _cacheDirectory);
                if (File.Exists(path))
                {
                    dictionaryToPersist[kvp.Key] = fileName;
                }
            }
        }

        var json = JsonSerializer.Serialize(dictionaryToPersist, Extension.DefaultJsonSerializerOptions);
        var old = await File.ReadAllTextAsync(_indexPath);
        if (old == json)
        {
            Trace.WriteLine("[Cache] Index unchanged, skipping persist.");
            return;
        }

        await File.WriteAllTextAsync(_indexPath, json);
        Trace.WriteLine($"[Cache] Persisted {dictionaryToPersist.Count} items to index.");
    }

    public async Task<string> GetOrCreateAsync(string url)
    {
        var asyncLoader = _cache.GetOrAdd(url, key =>
        {
            return new AsyncLazyFile(async () =>
            {
                using (var cancellationTokenSource = new CancellationTokenSource(5000))
                {
                    var cancellationToken = cancellationTokenSource.Token;
                    using (var message = await new HttpClient().GetAsync(url, cancellationToken))
                    {
                        message.EnsureSuccessStatusCode();
                        var mediaType = message.Content.Headers.ContentType?.MediaType;
                        string extension = String.Empty;
                        if (!string.IsNullOrEmpty(mediaType))
                        {
                            try
                            {
                                extension = MimeTypeMap.GetExtension(mediaType, false);
                            }
                            catch (Exception e)
                            {
                                Trace.TraceError(
                                    $"[Cache] Error getting extension for MIME type '{mediaType}': {e.Message}");
                            }
                        }

                        if (string.IsNullOrEmpty(extension))
                        {
                            extension = Path.GetExtension(url);
                        }

                        if (string.IsNullOrEmpty(extension))
                        {
                            extension = ".unknown";
                        }

                        await using (var stream = await message.Content.ReadAsStreamAsync(CancellationToken.None))
                        {
                            var directoryInfo = new DirectoryInfo(_cacheDirectory);
                            if (!directoryInfo.Exists)
                            {
                                directoryInfo.Create();
                            }

                            var newGuid = Guid.NewGuid();
                            var fileName = $"{newGuid}{extension}";
                            var fullPath = Path.GetFullPath(fileName, _cacheDirectory);
                            await using (var fileStream =
                                         new FileInfo(fullPath).Open(FileMode.Create, FileAccess.Write))
                            {
                                await stream.CopyToAsync(fileStream, CancellationToken.None);
                            }

                            return fileName;
                        }
                    }
                }
            });
        });
        string filePath;
        try
        {
            var fileName = await asyncLoader;
            filePath = Path.GetFullPath(fileName, _cacheDirectory);
        }
        catch (Exception)
        {
            _cache.TryRemove(url, out _);
            throw;
        }

        // 检查文件是否存在，处理索引与物理文件不一致的情况
        if (!File.Exists(filePath))
        {
            Trace.WriteLine(
                $"[Thread {Environment.CurrentManagedThreadId}] File '{filePath}' not found on disk. Invalidating and retrying.");
            _cache.TryRemove(url, out _);
            // 递归调用，触发一次全新的下载流程
            return await GetOrCreateAsync(url);
        }

        return filePath;
    }

    public void Clear()
    {
        //清空所有键值对和本地缓存
        _cache.Clear();
        //删除缓存目录下的所有文件
        if (Directory.Exists(_cacheDirectory))
        {
            try
            {
                Directory.Delete(_cacheDirectory, true);
            }
            catch (Exception ex)
            {
                Trace.TraceError($"[Cache] Error clearing cache directory: {ex.Message}");
            }
        }
    }

    public static HttpContentCache Instance
    {
        get { return FileCacheLazy.Value; }
    }

    private static readonly Lazy<HttpContentCache> FileCacheLazy = new Lazy<HttpContentCache>(() =>
    {
        return new HttpContentCache(CacheFolderPath);
    });

    public class AsyncLazyFile
    {
        private readonly Lazy<Task<string>> _instance;

        public AsyncLazyFile(Func<Task<string>> factory)
        {
            _instance = new Lazy<Task<string>>(factory, LazyThreadSafetyMode.ExecutionAndPublication);
        }

        public Task<string> Value => _instance.Value;

        public TaskAwaiter<string> GetAwaiter()
        {
            return Value.GetAwaiter();
        }
    }
}