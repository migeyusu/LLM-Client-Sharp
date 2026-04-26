using LLMClient.Abstraction;
using LLMClient.Agent.MiniSWE;

namespace LLMClient.Dialog.Models;

/// <summary>
/// local session with working directory and platform information.
/// </summary>
public interface IProjectSession : ISession
{
    string? WorkingDirectory { get; }

    RunPlatform Platform { get; }
    
    string ProjectInformationPrompt { get; }
    
    IAIFunctionGroup[] ProjectTools { get; }
}
