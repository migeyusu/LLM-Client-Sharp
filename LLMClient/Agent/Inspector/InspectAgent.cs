using System.ComponentModel;
using LLMClient.Abstraction;
using LLMClient.Agent.MiniSWE;
using LLMClient.Dialog;

namespace LLMClient.Agent.Inspector;

/// <summary>
/// Inspector agent that gathers structured project and code context for downstream agents.
/// It is intentionally read-only and only exposes inspection-safe tool groups.
/// </summary>
[Description("Inspect Agent")]
public class InspectAgent : ReadOnlyCompactAgentBase
{
    private const string InspectionCompleteFlag = "INSPECTION_COMPLETE";

    public InspectAgent(ILLMChatClient agent, AgentConfig agentConfig)
        : base(agent, agentConfig, CreateConfig(agent, agentConfig))
    {
    }

    public override string Name { get; } = "Inspect Agent";


    private static MiniSweAgentConfig CreateConfig(ILLMChatClient agent, AgentConfig agentConfig)
    {
        var config = CreateBaseConfig(agent, agentConfig);

        config.TaskCompleteFlag = InspectionCompleteFlag;
        config.IncludeToolInstructions = true;
        config.IncludeRagInstructions = true;
        config.StepLimit = 8;
        config.SystemTemplate = """
            You are an Inspector agent in a multi-agent software workflow.

            Your responsibility is to understand the task, collect the minimum necessary context,
            and hand off a reliable investigation summary for downstream agents.

            Core rules:
            - You are read-only. Do not modify files, create files, or apply patches.
            - Prefer structured code intelligence tools over ad-hoc shell inspection.
            - Start broad, then narrow down based on evidence.
            - Use project-awareness, code-reading, symbol-analysis, and code-search tools to understand the workspace.
            - Use CLI only for safe inspection tasks such as git status, directory inspection, or non-destructive project metadata commands.
            - Do not guess. Trace symbols and files before concluding.
            - When enough context has been gathered, produce a concise final inspection report and include the flag INSPECTION_COMPLETE.

            Expected output focus:
            - relevant projects / modules
            - likely files and symbols
            - important dependencies and call paths
            - uncertainties or missing context

            __PROJECT_CONTEXT_PLACEHOLDERS__
            """.Replace("__PROJECT_CONTEXT_PLACEHOLDERS__", BuildProjectContextPlaceholders());
        config.InstanceTemplate = """
            Please inspect the following task and gather the context needed by later agents.

            <task>
            {{task}}
            </task>

            Inspection workflow:
            1. Identify the relevant project or subsystem.
            2. Explore structure and conventions.
            3. Search for candidate files, types, and symbols.
            4. Read only the most relevant code or symbol bodies.
            5. Summarize findings, risks, and next implementation targets.

            __TOOL_PRIORITIES__

            Finish once you can provide an actionable inspection summary for another agent.
            Your final response must include INSPECTION_COMPLETE.
            """.Replace("__TOOL_PRIORITIES__", BuildReadOnlyToolPriorities());
        return config;
    }

}
