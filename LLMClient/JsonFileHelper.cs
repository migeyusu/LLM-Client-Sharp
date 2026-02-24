using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace LLMClient;

/// <summary>
/// 提供线程安全、原子性的 JSON 文件保存扩展方法。
/// </summary>
public static class JsonFileHelper
{
    // 使用 ConcurrentDictionary 存储每个文件路径对应的 SemaphoreSlim
    // 这样可以确保：不同文件的保存操作互不阻塞，但同一文件的保存操作会串行化
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> FileLocks = new();

    /// <summary>
    /// 将对象异步保存为 JSON 文件。支持 JsonNode 和普通类实例。
    /// 包含原子性写入机制（先写临时文件，成功后替换原文件）。
    /// </summary>
    /// <param name="data">要保存的数据对象</param>
    /// <param name="filePath">目标文件路径</param>
    /// <param name="options">JSON 序列化选项（可选）</param>
    public static async Task SaveJsonToFileAsync<T>(this T data, string filePath,
        JsonSerializerOptions? options = null)
    {
        // 获取该文件路径专用的锁
        var semaphore = FileLocks.GetOrAdd(filePath, _ => new SemaphoreSlim(1, 1));

        await semaphore.WaitAsync();
        try
        {
            await InternalSaveAsync(data, filePath, options);
        }
        finally
        {
            semaphore.Release();
        }
    }

    private static async Task InternalSaveAsync<T>(T data, string filePath, JsonSerializerOptions? options)
    {
        // 1. 准备目录
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // 2. 生成临时文件路径
        // 使用 .tmp 后缀，并在同一目录下
        string tempFilePath = filePath + ".tmp";

        try
        {
            // 3. 写入临时文件
            await using (var fileStream =
                         new FileStream(tempFilePath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                // 特殊处理 JsonNode，因为它有更高效的 WriteTo 方法
                if (data is JsonNode jsonNode)
                {
                    // 如果用户没有传 options，我们需要给 JsonWriter 一个默认配置，或者继承 options 的配置
                    // 这里的 Indented 设置参考了常见的默认配置习惯，也可以强制使用 options
                    var writerOptions = new JsonWriterOptions
                    {
                        Indented = options?.WriteIndented ?? false
                    };

                    await using (var utf8JsonWriter = new Utf8JsonWriter(fileStream, writerOptions))
                    {
                        jsonNode.WriteTo(utf8JsonWriter);
                        await utf8JsonWriter.FlushAsync();
                    }
                }
                else
                {
                    // 普通 POCO 类序列化
                    await JsonSerializer.SerializeAsync(fileStream, data, options);
                }
            }

            // 4. 原子性替换文件
            // 如果这一步成功，原文件被瞬间替换（这在 Windows 上是原子操作）。
            File.Move(tempFilePath, filePath, true);
        }
        catch
        {
            // 发生任何异常，尝试清理可能残留的临时文件
            if (File.Exists(tempFilePath))
            {
                try
                {
                    File.Delete(tempFilePath);
                }
                catch
                {
                    /* 忽略清理时的二次错误 */
                }
            }

            throw; // 重新抛出异常供上层捕获
        }
    }
}