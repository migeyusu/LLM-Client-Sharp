using System.Diagnostics;

namespace LLMScripts;

/// <summary>
/// 绕过加密app
/// </summary>
public class EncryptRead
{
    private static string SafeReadyPs(string filePath)
    {
        string command = $"Get-Content -Path '{filePath}'";
        var psi = new ProcessStartInfo
        {
            // Windows PowerShell 5.x
            FileName = "powershell",
            Arguments = $"-NoProfile -NonInteractive -Command \"{command}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false, // 必须为 false，才能重定向输出
            CreateNoWindow = true // 不弹出黑框
        };

        using var proc = Process.Start(psi)
                         ?? throw new InvalidOperationException("无法启动 PowerShell 进程。");

        string output = proc.StandardOutput.ReadToEnd();
        string error = proc.StandardError.ReadToEnd();

        proc.WaitForExit();

        if (proc.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"PowerShell 退出码 {proc.ExitCode}，错误信息：{error}");
        }

        return output.Trim();
    }
}