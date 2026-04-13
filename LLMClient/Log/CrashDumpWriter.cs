using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace LLMClient.Log;

public static class CrashDumpWriter
{
    private const string CrashDumpDirectoryName = "CrashDumps";

    public static string GetDumpDirectoryPath(string logRootPath)
    {
        return Path.Combine(logRootPath, CrashDumpDirectoryName);
    }

    public static bool TryWriteCurrentProcessDump(string logRootPath, out string? dumpPath, out Exception? error)
    {
        dumpPath = null;
        error = null;

        try
        {
            var dumpDirectory = GetDumpDirectoryPath(logRootPath);
            Directory.CreateDirectory(dumpDirectory);

            dumpPath = Path.Combine(
                dumpDirectory,
                $"crash_{DateTime.Now:yyyyMMdd_HHmmss_fff}_{Environment.ProcessId}.dmp");

            using var process = Process.GetCurrentProcess();
            return TryWriteDump(process, dumpPath, out error);
        }
        catch (Exception ex)
        {
            error = ex;
            return false;
        }
    }

    internal static bool TryWriteDump(Process process, string dumpPath, out Exception? error)
    {
        error = null;

        try
        {
            using var stream = new FileStream(dumpPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
            var dumpType = MiniDumpType.MiniDumpWithDataSegs |
                           MiniDumpType.MiniDumpWithHandleData |
                           MiniDumpType.MiniDumpWithUnloadedModules |
                           MiniDumpType.MiniDumpWithThreadInfo;
            var succeeded = MiniDumpWriteDump(
                process.Handle,
                (uint)process.Id,
                stream.SafeFileHandle.DangerousGetHandle(),
                dumpType,
                IntPtr.Zero,
                IntPtr.Zero,
                IntPtr.Zero);

            if (succeeded)
            {
                return true;
            }

            error = new Win32Exception(Marshal.GetLastWin32Error(), "MiniDumpWriteDump failed.");
            return false;
        }
        catch (Exception ex)
        {
            error = ex;
            return false;
        }
    }

    [DllImport("Dbghelp.dll", SetLastError = true)]
    private static extern bool MiniDumpWriteDump(
        IntPtr hProcess,
        uint processId,
        IntPtr hFile,
        MiniDumpType dumpType,
        IntPtr exceptionParam,
        IntPtr userStreamParam,
        IntPtr callbackParam);

    [Flags]
    private enum MiniDumpType : uint
    {
        MiniDumpWithDataSegs = 0x00000001,
        MiniDumpWithHandleData = 0x00000004,
        MiniDumpWithUnloadedModules = 0x00000020,
        MiniDumpWithThreadInfo = 0x00001000
    }
}

