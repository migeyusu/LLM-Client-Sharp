using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace LLMClient.Data;

public class PromptsCache
{
    private static readonly string CacheFolderName = "Cache" + Path.DirectorySeparatorChar + "Prompts";
    public static string CacheFolderPath => Path.GetFullPath(CacheFolderName);

    private ConcurrentDictionary<string, string> _cache;

    private readonly string _filePath;

    public string ModelId { get; }

    public string EndpointId { get; }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="cacheId">id for identify sessions</param>
    /// <param name="cacheDirectory"></param>
    /// <param name="endpointId"></param>
    /// <param name="modelId"></param>
    public PromptsCache(string cacheId, string cacheDirectory, string endpointId, string modelId)
    {
        ModelId = modelId;
        EndpointId = endpointId;
        _cache = new ConcurrentDictionary<string, string>();
        if (string.IsNullOrEmpty(cacheId))
        {
            throw new ArgumentException("id cannot be null or empty", nameof(cacheId));
        }

        if (string.IsNullOrEmpty(cacheDirectory))
        {
            throw new ArgumentException("cacheDirectory cannot be null or empty", nameof(cacheDirectory));
        }

        _filePath = Path.Combine(cacheDirectory, cacheId);
    }

    public async Task InitializeAsync()
    {
        if (!File.Exists(_filePath))
        {
            Trace.TraceInformation("Prompt cache file does not exist: {0}", _filePath);
            return;
        }

        try
        {
            var textAsync = await File.ReadAllTextAsync(_filePath);
            var jsonNode = JsonNode.Parse(textAsync);
            if (jsonNode == null)
            {
                return;
            }

            var modeId = jsonNode[nameof(ModelId)];
            if (modeId == null || modeId.GetValue<string>() != ModelId)
            {
                Trace.TraceInformation("ModelId mismatch: expected {0}, found {1}", ModelId, modeId);
                return;
            }

            var endPoint = jsonNode[nameof(EndpointId)];
            if (endPoint == null || endPoint.GetValue<string>() != EndpointId)
            {
                Trace.TraceInformation("EndpointId mismatch: expected {0}, found {1}", EndpointId, endPoint);
                return;
            }

            var dictionary = JsonSerializer.Deserialize<Dictionary<string, string>>(textAsync);
            if (dictionary != null)
            {
                this._cache = new ConcurrentDictionary<string, string>(dictionary);
            }

            Trace.TraceInformation("Loaded prompt cache from: {0}", _filePath);
        }
        finally
        {
            Trace.TraceError("Failed to load prompt cache from: {0}", _filePath);
        }
    }

    public async Task SaveAsync()
    {
        try
        {
            var directory = Path.GetDirectoryName(_filePath);
            if (directory != null)
            {
                Directory.CreateDirectory(directory);
            }

            var options = Extension.DefaultJsonSerializerOptions;
            var jsonObject = new JsonObject
            {
                [nameof(ModelId)] = ModelId,
                [nameof(EndpointId)] = EndpointId,
                ["Prompts"] = JsonSerializer.SerializeToNode(_cache, options)
            };
            var jsonString = jsonObject.ToJsonString(options);
            await File.WriteAllTextAsync(_filePath, jsonString);
            Trace.TraceInformation("Saved prompt cache to: {0}", _filePath);
        }
        catch (Exception e)
        {
            Trace.TraceError("Failed to save prompt cache: {0}", e.Message);
        }
    }

    public bool TryGetValue(string key, [NotNullWhen(true)] out string? value)
    {
        return _cache.TryGetValue(key, out value);
    }

    public bool TryAdd(string key, string value)
    {
        return _cache.TryAdd(key, value);
    }
}