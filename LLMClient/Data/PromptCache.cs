using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace LLMClient.Data;

public class PromptsCache
{
    public static PromptsCache NoCache { get; } =
        new PromptsCache("NoCache", Path.GetTempPath());

    private static readonly string CacheFolderName = "Cache" + Path.DirectorySeparatorChar + "Prompts";
    public static string CacheFolderPath => Path.GetFullPath(CacheFolderName);

    private ConcurrentDictionary<string, string> _cache;

    private readonly string _filePath;

    public string? ModelId { get; private set; }

    public string? EndpointId { get; private set; }

    public int? OutputSize { get; set; }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="cacheId">id for identify sessions</param>
    /// <param name="cacheDirectory"></param>
    public PromptsCache(string cacheId, string cacheDirectory)
    {
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

    public bool IsLoaded { get; private set; }

    public async Task LoadAsync(string endpointId, string modelId, int outputSize)
    {
        if (IsLoaded)
        {
            return;
        }

        IsLoaded = true;
        ModelId = modelId;
        EndpointId = endpointId;
        OutputSize = outputSize;
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

            var model = jsonNode[nameof(ModelId)];
            if (model == null || model.GetValue<string>() != modelId)
            {
                Trace.TraceInformation("ModelId mismatch: expected {0}, found {1}", ModelId, model);
                return;
            }

            var endPoint = jsonNode[nameof(EndpointId)];
            if (endPoint == null || endPoint.GetValue<string>() != endpointId)
            {
                Trace.TraceInformation("EndpointId mismatch: expected {0}, found {1}", EndpointId, endPoint);
                return;
            }

            var output = jsonNode[nameof(OutputSize)];
            if (output == null || output.GetValue<int>() != outputSize)
            {
                Trace.TraceInformation("SummarySize mismatch: expected {0}, found {1}", OutputSize.Value,
                    output);
                return;
            }

            var node = jsonNode["Prompts"];
            if (node == null)
            {
                Trace.TraceInformation("No prompts found in cache file: {0}", _filePath);
                return;
            }

            var dictionary = node.Deserialize<Dictionary<string, string>>();
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
            if (OutputSize != null)
            {
                jsonObject[nameof(OutputSize)] = OutputSize.Value;
            }

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

    public void AddOrUpdate(string key, string value)
    {
        _cache.AddOrUpdate(key, value, (k, v) => value);
    }

    public void Clear()
    {
        _cache.Clear();
    }
}