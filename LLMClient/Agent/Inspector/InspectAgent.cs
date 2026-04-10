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
    private const string CompactSeparator = "[INSPECT_COMPACT_HANDOFF]";

    public InspectAgent(ILLMChatClient agent, AgentOption agentOption)
        : base(agent, agentOption, CreateConfig(agent, agentOption))
    {
    }

    public override string Name { get; } = "Inspect Agent";

    protected override string TaskCompleteFlag => InspectionCompleteFlag;

    protected override string CompactHandoffSeparator => CompactSeparator;

    protected override string CompactErrorTag => "InspectCompact";

    protected override string CompactPromptTemplate => """
                                                        You are a strict inspection compactor. Output JSON only.

                                                        # Goal
                                                        You will receive indexed loop outputs produced by an inspector agent.
                                                        Remove loops that are irrelevant, repetitive, or mostly tool-call noise,
                                                        then produce a compact inspection handoff for downstream coding agents.

                                                        # Task
                                                        {{$task}}

                                                        # Context Hint
                                                        {{$contextHint}}

                                                        # Rules
                                                        1. Return JSON only.
                                                        2. JSON shape must be: { "removeIndexes": [int], "summary": "string" }.
                                                        3. `removeIndexes` should contain loop indexes that can be discarded from downstream context.
                                                        4. Prefer removing loops that are duplicate exploration, dead-end searches, or pure tool chatter.
                                                        5. `summary` must keep only task-relevant findings: files, symbols, dependencies, call paths, risks, and uncertainties.
                                                        6. Do not invent facts. If something is uncertain, say so explicitly.
                                                        7. The summary must end with INSPECTION_COMPLETE.

                                                        # Indexed Loop Records
                                                        {{$input}}
                                                        """;

    private static MiniSweAgentConfig CreateConfig(ILLMChatClient agent, AgentOption agentOption)
    {
        var config = CreateBaseConfig(agent, agentOption);

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
