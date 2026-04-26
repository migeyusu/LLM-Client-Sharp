using LLMClient.Agent.MiniSWE;

namespace LLMClient.Dialog.Models;

/// <summary>
/// Project-specific session interface extending ITextDialogSession
/// with working directory and platform information.
/// </summary>
public interface IProject : IDialog
{
    string? WorkingDirectory { get; }

    RunPlatform Platform { get; }
}