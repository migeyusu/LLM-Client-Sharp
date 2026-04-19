using LLMClient.Abstraction;
using LLMClient.Dialog.Models;

namespace LLMClient.Agent;

public interface IAgent
{
    string Name { get; }

    /// <summary>
    /// 以 ReAct 循环流的形式执行 Agent。每个 ReactStep 代表一轮 Reasoning + Acting。
    /// </summary>
    IAsyncEnumerable<ReactStep> Execute(ITextDialogSession dialogSession,
        CancellationToken cancellationToken = default);
}

public interface IInbuiltAgent
{
    private static readonly Lazy<Type[]> ChildTypesLazy = new(() => typeof(IInbuiltAgent).ImplementsTypes().ToArray());

    static Type[] ChildTypes => ChildTypesLazy.Value;
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