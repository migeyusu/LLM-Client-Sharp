using LLMClient.Dialog.Models;
using LLMClient.Endpoints;

namespace LLMClient.Agent;

public interface IAgent
{
    string Name { get; }
    
    IAsyncEnumerable<ChatCallResult> Execute(ITextDialogSession dialogSession,
        CancellationToken cancellationToken = default);
}

/*
 * <agent_system>
...基础系统规则...
</agent_system>

<platform_instructions platform="windows">
...Windows 特定规则...
</platform_instructions>

<tool_instructions>
<tool name="FileSystem">
...
</tool>

<tool name="WinCLI">
...
</tool>
</tool_instructions>
 */