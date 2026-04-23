using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using LLMClient.Abstraction;
using LLMClient.Component.Utility;
using LLMClient.Endpoints;
using Microsoft.SemanticKernel;

namespace LLMClient.ToolCall.DefaultPlugins;

/// <summary>
/// A tool group that executes Linux shell commands inside WSL.
/// </summary>
public sealed class WslCLIPlugin : KernelFunctionGroup, IBuiltInFunctionGroup
{
    public HashSet<string> VerifyRequiredCommands { get; set; }

    public string WslDistributionName { get; set; } = string.Empty;

    public string WslUserName { get; set; } = string.Empty;

    public int TimeoutSeconds { get; set; } = 30;

    public bool MapWorkingDirectoryToWsl { get; set; } = true;

    public static List<string> DefaultDeniedCommands => new()
    {
        "rm", "unlink", "shred", "mkfs", "dd", "shutdown", "reboot", "halt", "poweroff",
        "curl", "wget",
        "sudo", "su",
        "chmod", "chown", "chgrp",
        "systemctl", "service", "kill", "pkill", "killall",
        "iptables", "ufw",
        "mount", "umount"
    };

    public WslCLIPlugin() : this(DefaultDeniedCommands)
    {
    }

    public WslCLIPlugin(IEnumerable<string> deniedCommands) : base("WslCLI")
    {
        VerifyRequiredCommands = new HashSet<string>(
            deniedCommands.Select(command => command.ToLowerInvariant()));
    }

    [KernelFunction]
    [Description("Executes a bash command inside WSL.")]
    public async Task<ExecutionOutput> ExecuteCommandAsync(
        Kernel kernel,
        [Description("The full bash command to execute inside WSL.")]
        string command,
        [Description("Optional working directory. Can be a Windows path or a WSL path.")]
        string workingDirectory = "",
        [Description("Optional distro name. Empty means the default WSL distro.")]
        string distribution = "",
        [Description("Optional user name. Empty means the default distro user.")]
        string user = "",
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            throw new ArgumentNullException(nameof(command), "错误：命令不能为空。");
        }

        var commandBase = command.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries)[0].ToLowerInvariant();
        if (VerifyRequiredCommands.Contains(commandBase))
        {
            var step = AsyncContextStore<ChatStackContext>.Current?.CurrentStep;
            if (step == null)
            {
                throw new NotSupportedException($"错误：命令 '{commandBase}' 被默认禁止执行。");
            }

            if (!await step.RequestPermissionAsync(
                    $"安全警告\n请求在 WSL 中执行命令：{command}\n该命令被列为危险命令，可能会对系统造成损害。是否继续？"))
            {
                throw new UnauthorizedAccessException("用户拒绝执行命令: " + command);
            }
        }

        try
        {
            var effectiveDistribution = string.IsNullOrWhiteSpace(distribution)
                ? WslDistributionName
                : distribution;
            var effectiveUser = string.IsNullOrWhiteSpace(user)
                ? WslUserName
                : user;

            var normalizedWorkingDirectory = NormalizeWorkingDirectory(workingDirectory);
            var shellCommand = BuildShellCommand(command, normalizedWorkingDirectory);
            var arguments = BuildWslArguments(shellCommand, effectiveDistribution, effectiveUser);

            var processStartInfo = new ProcessStartInfo
            {
                FileName = "wsl.exe",
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(TimeoutSeconds));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            var process = new Process
            {
                StartInfo = processStartInfo
            };

            process.Start();

            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();
            try
            {
                await Task.WhenAll(outputTask, errorTask, process.WaitForExitAsync(linkedCts.Token));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // User-initiated cancellation: kill the process tree
                try { process.Kill(entireProcessTree: true); } catch { /* ignore if already exited */ }
                return new ExecutionOutput
                {
                    Output = string.Empty,
                    ReturnCode = -1,
                    ExceptionInfo = "Command execution was cancelled by user.",
                    Extra = new Dictionary<string, object>
                    {
                        ["platform"] = "wsl"
                    }
                };
            }
            catch (OperationCanceledException)
            {
                // Timeout
                try { process.Kill(entireProcessTree: true); } catch { /* ignore if already exited */ }
                return new ExecutionOutput
                {
                    Output = string.Empty,
                    ReturnCode = -1,
                    ExceptionInfo = $"Command timed out after {TimeoutSeconds} seconds",
                    Extra = new Dictionary<string, object>
                    {
                        ["platform"] = "wsl"
                    }
                };
            }

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
                ExceptionInfo = process.ExitCode != 0
                    ? $"Command exited with code {process.ExitCode}"
                    : null,
                Extra = new Dictionary<string, object>
                {
                    ["shell"] = "bash",
                    ["platform"] = "wsl",
                    ["distribution"] = effectiveDistribution,
                    ["user"] = effectiveUser,
                    ["working_directory"] = normalizedWorkingDirectory ?? string.Empty
                }
            };
        }
        catch (Exception ex)
        {
            return new ExecutionOutput
            {
                Output = string.Empty,
                ReturnCode = -1,
                ExceptionInfo = $"Command exited with exception: {ex.Message}",
                Extra = new Dictionary<string, object>
                {
                    ["platform"] = "wsl"
                }
            };
        }
    }

    public override string? AdditionPrompt =>
        """
        This tool group executes Linux shell commands inside WSL.

        Execution environment:
        - Commands run in bash inside WSL, not in Windows PowerShell or CMD.
        - Prefer bash-compatible Linux commands.
        - Standard Unix tools may be available depending on the installed distro.
        - Do not use PowerShell syntax here.

        Important execution rules:
        1. Each command may run in an isolated process.
        2. Do not assume the current directory or exported environment variables persist across commands.
        3. If a command depends on a specific working directory, provide it explicitly.
        4. Prefer non-interactive commands and flags.

        Path guidance:
        - If the repository is located on the Windows filesystem, working directories may be mapped to /mnt/<drive>/...
        - Be careful when mixing Windows paths and Linux paths.
        - Prefer Linux-style paths when writing shell commands for this tool.

        When working with source code:
        - Prefer the FileSystem tool group for file reading and editing.
        - Prefer WslCLI for building, testing, running scripts, invoking Linux tooling, and shell-based verification.
        """;

    public override object Clone()
    {
        return new WslCLIPlugin()
        {
            VerifyRequiredCommands = new HashSet<string>(VerifyRequiredCommands),
            WslDistributionName = WslDistributionName,
            WslUserName = WslUserName,
            TimeoutSeconds = TimeoutSeconds,
            MapWorkingDirectoryToWsl = MapWorkingDirectoryToWsl
        };
    }

    private string? NormalizeWorkingDirectory(string? workingDirectory)
    {
        if (string.IsNullOrWhiteSpace(workingDirectory))
        {
            return null;
        }

        if (!MapWorkingDirectoryToWsl)
        {
            return workingDirectory;
        }

        return WslPathUtility.NormalizeToWslPath(workingDirectory);
    }

    private static string BuildShellCommand(string command, string? workingDirectory)
    {
        if (string.IsNullOrWhiteSpace(workingDirectory))
        {
            return command;
        }

        return $"cd '{EscapeForSingleQuotedBash(workingDirectory)}' && {command}";
    }

    private static string BuildWslArguments(string shellCommand, string? distribution, string? user)
    {
        var builder = new StringBuilder();

        if (!string.IsNullOrWhiteSpace(distribution))
        {
            builder.Append("--distribution ");
            builder.Append(QuoteWindowsArgument(distribution));
            builder.Append(' ');
        }

        if (!string.IsNullOrWhiteSpace(user))
        {
            builder.Append("--user ");
            builder.Append(QuoteWindowsArgument(user));
            builder.Append(' ');
        }

        builder.Append("--exec bash -lc ");
        builder.Append(QuoteWindowsArgument(shellCommand));

        return builder.ToString();
    }

    private static string QuoteWindowsArgument(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "\"\"";
        }

        return "\"" + value.Replace("\"", "\\\"") + "\"";
    }

    private static string EscapeForSingleQuotedBash(string value)
    {
        return value.Replace("'", "'\"'\"'");
    }
}
