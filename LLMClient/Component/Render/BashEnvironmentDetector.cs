using System.Diagnostics;
using MaterialDesignThemes.Wpf;

namespace LLMClient.Component.Render;

public class BashEnvironment
{
    public required string Name { get; set; }
    public required string ExecutablePath { get; set; }
    public required PackIconKind Icon { get; set; } // 只是为了UI好看，可以用字体图标或简单的字符

    public required BashEnvironmentType EnvironmentType { get; set; }
}

public enum BashEnvironmentType
{
    Cmd,
    GitBash,
    Wsl
}

public static class BashEnvironmentDetector
{
    private static List<BashEnvironment>? _cachedEnvironments;

    public static List<BashEnvironment> GetAvailableEnvironments()
    {
        // 如果不需要缓存（比如允许用户在程序运行时安装Git），可以去掉判空直接执行
        if (_cachedEnvironments != null) return _cachedEnvironments;

        var list = new List<BashEnvironment>();

        // 1. 通过 where wsl 检测 WSL
        // wsl.exe 是现代 Windows 推荐的入口
        var wslPaths = GetPathsFromWhere("wsl");
        if (wslPaths.Any())
        {
            // 通常取第一个找到的
            list.Add(new BashEnvironment
            {
                Name = "WSL (Default Distro)",
                ExecutablePath = wslPaths.First(),
                Icon = PackIconKind.Linux, // WSL/Linux Icon
                EnvironmentType =  BashEnvironmentType.Wsl
            });
        }

        // 2. 通过 where bash 检测 Git Bash
        var bashPaths = GetPathsFromWhere("git");
        foreach (var path in bashPaths)
        {
            // Windows System32 下也有一个 bash.exe (WSL Legacy)，为了避免重复，
            // 我们通常只认定路径包含 "Git" 的为 Git Bash。
            if (path.IndexOf("Git", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                list.Add(new BashEnvironment
                {
                    Name = "Git Bash",
                    ExecutablePath = path,
                    Icon = PackIconKind.Git, // Git Icon
                    EnvironmentType = BashEnvironmentType.GitBash
                });

                // 只需要找到一个 Git Bash 即可，避免 usr/bin 和 bin 下重复的 bash.exe 造成列表冗余
                break;
            }
        }

        //3. 默认的cmd
        list.Add(new BashEnvironment()
        {
            Name = "Cmd",
            ExecutablePath = "cmd.exe",
            Icon = PackIconKind.MicrosoftWindows,
            EnvironmentType = BashEnvironmentType.Cmd,
        });

        _cachedEnvironments = list;
        return list;
    }

    /// <summary>
    /// 执行 system 'where' 命令查找可执行文件路径
    /// </summary>
    private static List<string> GetPathsFromWhere(string executableName)
    {
        var paths = new List<string>();
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c where {executableName}",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var process = Process.Start(psi);
            if (process != null)
            {
                // 读取所有输出行
                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output))
                {
                    // where 命令可能返回多行（如果有多个路径）
                    var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var line in lines)
                    {
                        if (File.Exists(line.Trim()))
                        {
                            paths.Add(line.Trim());
                        }
                    }
                }
            }
        }
        catch
        {
            // 忽略执行过程中的任何错误（如权限不足等），返回空列表
        }

        return paths;
    }
}