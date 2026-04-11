using System.ComponentModel;
using LLMClient.Abstraction;
using LLMClient.Agent.MiniSWE;
using LLMClient.Dialog;

namespace LLMClient.Agent.Planner;

/// <summary>
/// Planner agent that iterates between context inspection and plan synthesis for downstream coding execution.
/// It is read-only and only exposes inspection-safe tool groups.
/// </summary>
[Description("Planner Agent")]
public class PlannerAgent : ReadOnlyCompactAgentBase
{
    private const string PlanningCompleteFlag = "PLANNING_COMPLETE";

    public PlannerAgent(ILLMChatClient agent, AgentOption agentOption)
        : base(agent, agentOption, CreateConfig(agent, agentOption))
    {
    }

    public override string Name { get; } = "Planner Agent";


    private static MiniSweAgentConfig CreateConfig(ILLMChatClient agent, AgentOption agentOption)
    {
        var config = CreateBaseConfig(agent, agentOption);

        config.TaskCompleteFlag = PlanningCompleteFlag;
        config.IncludeToolInstructions = true;
        config.IncludeRagInstructions = true;
        config.StepLimit = 10;
        config.SystemTemplate = """
            You are a Planner agent in a multi-agent software workflow.

            Your responsibility is to inspect the codebase, build an executable plan,
            and hand off a reliable implementation roadmap for downstream coding agents.

            Core rules:
            - You are read-only. Do not modify files, create files, or apply patches.
            - Integrate context inspection and planning in one iterative loop.
            - Prefer structured code intelligence tools over ad-hoc shell inspection.
            - Use project-awareness, code-reading, symbol-analysis, and code-search tools to gather evidence.
            - Use CLI only for safe inspection tasks such as git status, directory inspection, or non-destructive project metadata commands.
            - Do not guess. Trace symbols and files before proposing actions.
            - Inherit prior Inspector outputs from conversation history when available.
            - If history already includes inspection handoff markers like INSPECTION_COMPLETE or [INSPECT_COMPACT_HANDOFF],
              reuse that context first and only run additional inspection tools to fill clear gaps.
            - When enough context has been gathered, provide an actionable phased plan and include PLANNING_COMPLETE.

            Expected output focus:
            - relevant projects / modules
            - key files and symbols to touch
            - implementation sequence and dependencies
            - risks, assumptions, and validation strategy

            __PROJECT_CONTEXT_PLACEHOLDERS__
            """.Replace("__PROJECT_CONTEXT_PLACEHOLDERS__", BuildProjectContextPlaceholders());
        config.InstanceTemplate = """
            Please plan the following task by combining context inspection and planning.

            <task>
            {{task}}
            </task>

            Context inheritance:
            - First read and trust relevant Inspector handoff from prior conversation history.
            - Treat prior inspection summary as baseline context for planning.
            - Only re-inspect files/symbols when the inherited context is missing, ambiguous, or stale.

            Planning workflow:
            1. Identify relevant project scope and constraints.
            2. Inspect structure, conventions, and existing implementations.
            3. Locate candidate files, types, and symbols.
            4. Analyze dependencies and likely impact surface.
            5. Produce a phased implementation plan with risks and verification steps.

            __TOOL_PRIORITIES__

            Finish once you can provide an actionable plan for another coding agent.
            Your final response must include PLANNING_COMPLETE.
            """.Replace("__TOOL_PRIORITIES__", BuildReadOnlyToolPriorities());
        return config;
    }

}
