using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LLMClient.ContextEngineering;

public class InfoCacheManager
{
    private readonly string _cacheDirectory;
    private readonly TimeSpan _cacheDuration;
    private readonly JsonSerializerOptions _jsonOptions;

    public InfoCacheManager(string cacheDir = ".aipilot/cache", TimeSpan? duration = null)
    {
        _cacheDirectory = Path.GetFullPath(cacheDir);
        _cacheDuration = duration ?? TimeSpan.FromHours(1);
        Directory.CreateDirectory(_cacheDirectory);
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
    }

    public async Task<ProjectInfo> GetOrGenerateAsync(
        Microsoft.CodeAnalysis.Project project, string projectFilePath,
        Func<Task<ProjectInfo>> generator)
    {
        ProjectInfo? projectSummary = null;
        if (IsCacheValid(project, projectFilePath))
        {
            try
            {
                projectSummary = await LoadFromCacheAsync(projectFilePath);
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Failed to load cache: {ex.Message}");
            }
        }

        if (projectSummary != null)
            return projectSummary;
        var summary = await generator();
        await SaveToCacheAsync(projectFilePath, summary);
        return summary;
    }

    private bool IsCacheValid(Microsoft.CodeAnalysis.Project project, string projectFilePath)
    {
        var cachePath = GetCachePath(projectFilePath);
        if (!File.Exists(cachePath)) return false;

        var cacheInfo = new FileInfo(cachePath);
        var projectFileInfo = new FileInfo(projectFilePath);

        // 检查缓存是否比源文件新
        if (cacheInfo.LastWriteTimeUtc < projectFileInfo.LastWriteTimeUtc)
            return false;

        // 检查缓存是否过期
        if (DateTime.UtcNow - cacheInfo.LastWriteTimeUtc > _cacheDuration)
            return false;

        // 检查相关项目文件是否有更新
        var projectDir = Path.GetDirectoryName(projectFilePath);
        var projectFiles = Directory.GetFiles(projectDir, "*.csproj", SearchOption.AllDirectories)
            .Concat(Directory.GetFiles(projectDir, "*.cs", SearchOption.AllDirectories))
            .Where(f => !f.Contains("\\obj\\") && !f.Contains("\\bin\\"));

        foreach (var file in projectFiles.Take(100)) // 只检查前100个文件以提高性能
        {
            if (File.GetLastWriteTimeUtc(file) > cacheInfo.LastWriteTimeUtc)
                return false;
        }

        return true;
    }

    public async Task<ProjectInfo?> LoadFromCacheAsync(string solutionPath)
    {
        var cachePath = GetCachePath(solutionPath);
        await using var stream = File.OpenRead(cachePath);
        return await JsonSerializer.DeserializeAsync<ProjectInfo>(stream, _jsonOptions);
    }

    public async Task SaveToCacheAsync(string projectPath, ProjectInfo summary)
    {
        var cachePath = GetCachePath(projectPath);
        var json = JsonSerializer.Serialize(summary, _jsonOptions);
        await File.WriteAllTextAsync(cachePath, json);
    }

    public void InvalidateCache(string solutionPath)
    {
        var cachePath = GetCachePath(solutionPath);
        if (File.Exists(cachePath))
            File.Delete(cachePath);
    }

    private string GetCachePath(string projectFilePath)
    {
        var hash = GetFileHash(projectFilePath);
        return Path.Combine(_cacheDirectory, $"{hash}_summary.json");
    }

    private static string GetFileHash(string path)
    {
        using var sha256 = SHA256.Create();
        var pathBytes = Encoding.UTF8.GetBytes(Path.GetFullPath(path).ToLowerInvariant());
        var hashBytes = sha256.ComputeHash(pathBytes);
        return Convert.ToHexString(hashBytes).Substring(0, 16);
    }
}