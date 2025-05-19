using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace LLMClient;

public static class DebugEx
{
    public static void WriteLine(string message, [CallerLineNumber] int lineNumber = 0,
        [CallerMemberName] string? caller = null)
    {
        Debug.WriteLine(message + " at line " + lineNumber + " (" + caller + ")");
    }

    public static void PrintThreadId([CallerLineNumber] int lineNumber = 0,
        [CallerMemberName] string? caller = null)
    {
        Debug.WriteLine("Thread ID: " + Environment.CurrentManagedThreadId + " at line " + lineNumber + " (" + caller +
                        ")");
    }
}