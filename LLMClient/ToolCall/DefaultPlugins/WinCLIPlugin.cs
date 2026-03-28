using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using LLMClient.Abstraction;
using LLMClient.Component.Utility;
using LLMClient.Endpoints;
using Microsoft.SemanticKernel;

namespace LLMClient.ToolCall.DefaultPlugins;

/// <summary>
/// 执行结果
/// </summary>
public class ExecutionOutput
{
    public string Output { get; set; } = string.Empty;
    public int ReturnCode { get; set; }
    public string? ExceptionInfo { get; set; }

    public Dictionary<string, object> Extra { get; set; } = new();
}

/// <summary>
/// (黑名单版本) 一个用于在 Windows 命令行 (CMD) 或 PowerShell 中执行命令的插件。
/// 注意：出于安全考虑，此插件采用“拒绝列表”（黑名单）机制。
/// 默认会阻止一组已知的危险命令，您可以自定义此列表。
/// </summary>
public sealed class WinCLIPlugin : KernelFunctionGroup, IBuiltInFunctionGroup
{
    public HashSet<string> VerifyRequiredCommands { get; set; }

    /// <summary>
    /// 获取一个默认的、用于阻止执行的危险命令列表。
    /// 包含文件/目录删除、格式化、关机、网络下载、进程启动等命令。
    /// </summary>
    public static List<string> DefaultDeniedCommands => new List<string>
    {
        // CMD
        "del", "erase", "format", "rd", "rmdir", "shutdown", "mklink",
        // PowerShell
        "remove-item", "rm", "ri", // rm 和 ri 是 Remove-Item 的别名
        "clear-content", "clc",
        "stop-process", "kill",
        "stop-service",
        "format-volume",
        "invoke-webrequest", "iwr", "curl", "wget", // 网络请求，可能下载恶意内容
        "invoke-restmethod", "irm",
        "invoke-expression", "iex", // 极度危险，可以执行任意字符串
        "start-process", "start", "saps", // 启动进程
        "set-itemproperty", "sp", // 修改注册表等
        "remove-itemproperty", "rp",
    };

    public WinCLIPlugin() : this(DefaultDeniedCommands)
    {
    }

    /// <summary>
    /// 初始化 WinCLIPlugin 实例。
    /// </summary>
    /// <param name="deniedCommands">
    /// 一个可选的、包含禁止执行的命令的列表。
    /// 如果不提供，将使用一个内置的默认危险命令列表 (DefaultDeniedCommands)。
    /// </param>
    public WinCLIPlugin(IEnumerable<string> deniedCommands) : base("WinCLI")
    {
        // 将所有禁止的命令转换为小写并存入 HashSet，便于快速、不区分大小写地查找
        VerifyRequiredCommands = new HashSet<string>(deniedCommands.Select(cmd => cmd.ToLowerInvariant()));
    }

    [KernelFunction]
    [Description(
        "Executes a command-line command in the specified shell (PowerShell or CMD). ")]
    public async Task<ExecutionOutput> ExecuteCommandAsync(
        Kernel kernel,
        [Description("The full command to execute, for example, 'Get-Process -Name chrome' or 'ipconfig /all'.")]
        string command,
        [Description("The type of shell to use. Can be 'powershell' or 'cmd'. Defaults to 'powershell'.")]
        string shell = "powershell")
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            throw new ArgumentNullException(nameof(command), "错误：命令不能为空。");
        }

        // 安全性检查：提取命令的第一个词（即程序名），并检查它是否在黑名单中
        // 注意：这种检查方式相对基础，聪明的用户可能通过复杂方式绕过。
        // 例如 'cmd /c "del C:\\file.txt"'，这里的检查只会看到 'cmd'。
        var commandBase = command.Trim().Split(' ')[0].ToLowerInvariant();
        if (VerifyRequiredCommands.Contains(commandBase))
        {
            var interactor = AsyncContextStore<ChatContext>.Current?.Interactor;
            if (interactor == null)
            {
                throw new NotSupportedException($"错误：命令 '{commandBase}' 被默认禁止执行。");
            }

            if (!await interactor.WaitForPermission("安全警告",
                    $"请求执行命令：{command}\n该命令被列为危险命令，可能会对系统造成损害。是否继续？"))
            {
                throw new UnauthorizedAccessException("用户拒绝执行命令: " + command);
            }
        }

        try
        {
            var processStartInfo = new ProcessStartInfo
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            switch (shell.ToLowerInvariant())
            {
                case "powershell":
                    processStartInfo.FileName = "powershell.exe";
                    processStartInfo.Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{command}\"";
                    break;
                case "cmd":
                    processStartInfo.FileName = "cmd.exe";
                    processStartInfo.Arguments = $"/c \"{command}\"";
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(shell),
                        $"错误：不支持的 shell 类型 '{shell}'。请使用 'powershell' 或 'cmd'。");
            }

            using (var process = new Process())
            {
                process.StartInfo = processStartInfo;
                process.Start();

                var outputTask = process.StandardOutput.ReadToEndAsync();
                var errorTask = process.StandardError.ReadToEndAsync();
                var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                await Task.WhenAll(outputTask, errorTask, process.WaitForExitAsync(cts.Token));
                var output = outputTask.Result;
                var error = errorTask.Result;
                var resultBuilder = new StringBuilder();
                if (!string.IsNullOrWhiteSpace(output))
                {
                    resultBuilder.AppendLine("--- 输出 ---");
                    resultBuilder.AppendLine(output.Trim());
                }

                if (!string.IsNullOrWhiteSpace(error))
                {
                    resultBuilder.AppendLine("--- 错误 ---");
                    resultBuilder.AppendLine(error.Trim());
                }

                return new ExecutionOutput
                {
                    Output = resultBuilder.ToString(),
                    ReturnCode = process.ExitCode,
                    ExceptionInfo = process.ExitCode != 0 ? $"Command exited with code {process.ExitCode}" : null
                };
            }
        }
        catch (Exception ex)
        {
            return new ExecutionOutput
            {
                Output = string.Empty,
                ReturnCode = -1,
                ExceptionInfo = $"Command exited with exception: {ex.Message}"
            };
        }
    }

    public override string? AdditionPrompt =>
        """
        This tool group executes Windows command-line commands.

        Preferred shell:
        - Prefer PowerShell syntax and Windows-native commands.
        - Use CMD only when PowerShell is not appropriate.
        - Do not assume a bash shell is available.

        Important execution rules:
        1. Each command may run in an isolated process.
        2. Do not assume the current directory or environment changes persist across commands.
        3. If a command depends on a specific working directory, include it explicitly in the command.
        4. Prefer non-interactive commands and flags.

        Windows guidance:
        - Use PowerShell commands such as Get-ChildItem, Select-String, Get-Content, Set-Location, Test-Path, and dotnet.
        - Prefer backslash-safe or quoted paths when necessary.
        - Be careful with spaces in paths; quote them explicitly.
        - Avoid Unix-specific tools such as sed, awk, grep, ls, cat, or nl unless you know they are available in the current environment.

        When working with source code:
        - Prefer the FileSystem tool group for file reading and editing.
        - Prefer WinCLI for building, testing, searching with project tools, running scripts, and invoking platform-specific tooling.
        """;

    public override object Clone()
    {
        return new WinCLIPlugin()
        {
            VerifyRequiredCommands = new HashSet<string>(VerifyRequiredCommands)
        };
    }
}