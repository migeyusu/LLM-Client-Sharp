namespace LLMClient.ToolCall.DefaultPlugins;

public static class WslPathUtility
{
    public static string? NormalizeToWslPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        if (path.StartsWith("/", StringComparison.Ordinal))
        {
            return path;
        }

        return ConvertWindowsPathToWslPath(path);
    }

    public static string ConvertWindowsPathToWslPath(string windowsPath)
    {
        if (string.IsNullOrWhiteSpace(windowsPath))
        {
            return string.Empty;
        }

        var fullPath = Path.GetFullPath(windowsPath);
        var root = Path.GetPathRoot(fullPath);

        if (string.IsNullOrWhiteSpace(root) || root.Length < 2 || root[1] != ':')
        {
            return fullPath.Replace('\\', '/');
        }

        var driveLetter = char.ToLowerInvariant(root[0]);
        var relativePath = fullPath[root.Length..].Replace('\\', '/');

        return $"/mnt/{driveLetter}/{relativePath}";
    }
}
