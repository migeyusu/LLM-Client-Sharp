using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace LLMClient.Data;

public class HttpFileCache
{
    const string CacheFolderName = "Cache";

    const string CacheConfigFileName = "cache.json";

    private static string CacheFolderPath => Path.GetFullPath(CacheFolderName);

    private static string CacheConfigFilePath => Path.GetFullPath(CacheConfigFileName, CacheFolderPath);

    public Dictionary<string, string> UrlToFileName { get; set; } = new Dictionary<string, string>();

    public FileInfo? GetFile(string url)
    {
        if (UrlToFileName.TryGetValue(url, out var fileName))
        {
            var fullPath = Path.GetFullPath($"{fileName}.bin", CacheFolderPath);
            return new FileInfo(fullPath);
        }

        return null;
    }

    public void SaveContent(string url, Stream stream)
    {
        var directoryInfo = new DirectoryInfo(CacheFolderPath);
        if (!directoryInfo.Exists)
        {
            directoryInfo.Create();
        }

        var newGuid = Guid.NewGuid();
        var fullPath = Path.GetFullPath($"{newGuid}.bin", CacheFolderPath);
        using (var fileStream = new FileInfo(fullPath).Open(FileMode.Create, FileAccess.Write))
        {
            stream.CopyTo(fileStream);
        }

        UrlToFileName[url] = newGuid.ToString();
        var serialize = JsonSerializer.Serialize(this);
        File.WriteAllText(CacheConfigFilePath, serialize);
    }

    public static HttpFileCache Instance
    {
        get { return FileCacheLazy.Value; }
    }

    private static readonly Lazy<HttpFileCache> FileCacheLazy = new Lazy<HttpFileCache>(() =>
    {
        var fileInfo = new FileInfo(CacheConfigFilePath);
        if (fileInfo.Exists)
        {
            try
            {
                using (var fileStream = fileInfo.OpenRead())
                {
                    var httpFileCache = JsonSerializer.Deserialize<HttpFileCache>(fileStream);
                    if (httpFileCache != null)
                    {
                        return httpFileCache;
                    }
                }
            }
            catch (Exception e)
            {
                Trace.TraceError(e.Message);
            }
        }

        return new HttpFileCache();
    });
}