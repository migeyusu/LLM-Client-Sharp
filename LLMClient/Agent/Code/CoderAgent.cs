using System.ComponentModel;
using LLMClient.Abstraction;
using LLMClient.Agent.MiniSWE;
using LLMClient.Dialog;

namespace LLMClient.Agent.Code;

/// <summary>
/// Code Agent — A read-only agent that analyzes code and outputs code changes as text.
/// It only uses read-only tools and never modifies files.
/// The final output is presented as code blocks or diff format for the user to apply manually.
/// </summary>
[Description("Code Agent")]
public class CoderAgent : ReadOnlyCompactAgentBase
{
    private const string CodeCompleteFlag = "CODE_COMPLETE";

    public CoderAgent(ILLMChatClient agent, AgentConfig agentConfig)
        : base(agent, agentConfig, CreateConfig(agent, agentConfig))
    {
    }

    public override string Name { get; } = "Code Agent";

    private static MiniSweAgentConfig CreateConfig(ILLMChatClient agent, AgentConfig agentConfig)
    {
        var config = CreateBaseConfig(agent, agentConfig);

        // Override for Code Agent specific behavior
        config.TaskCompleteFlag = CodeCompleteFlag;
        config.IncludeToolInstructions = true;
        config.IncludeRagInstructions = true;
        config.StepLimit = 15; // Code generation may need more steps for complex tasks

        config.SystemTemplate = """
            You are a Code Agent — a read-only coding assistant that analyzes codebases and outputs code changes as text.

            CORE PRINCIPLE:
            - You MUST NOT modify, create, or delete any files.
            - You MUST ONLY output code in your final response.
            - All file operations are restricted to reading only.

            AVAILABLE TOOLS (all read-only):
            - FileSystem: Read files, search files, get directory structure
            - Code Intelligence: Project awareness, symbol analysis, code search, code reading
            - CLI: Environment inspection only (no file modifications)
            - Web: Search and fetch documentation

            OUTPUT FORMAT:
            When providing code changes, use one of these formats:

            1. For complete file replacement:
            ```file:path/to/file.cs
            // Full file content here
            ```

            2. For partial changes (diff-like):
            ```diff:path/to/file.cs
            // Old code to be replaced
            + // New code to add
            - // Code to remove
            ```

            3. For new file creation:
            ```newfile:path/to/newfile.cs
            // New file content
            ```

            RESPONSE STRUCTURE:
            1. Briefly explain what you need to do (1-2 sentences)
            2. Analyze the relevant code using read-only tools
            3. Output the code changes in the appropriate format above
            4. End with CODE_COMPLETE flag

            RULES:
            - Always inspect files before suggesting changes
            - Provide complete, syntactically correct code
            - Include necessary using statements and namespaces
            - Follow the project's coding conventions (inferred from existing code)
            - Be precise about file paths

            __PROJECT_CONTEXT_PLACEHOLDERS__
            """.Replace("__PROJECT_CONTEXT_PLACEHOLDERS__", BuildProjectContextPlaceholders());

        config.InstanceTemplate = """
            Please implement the following code change:

            <task>
            {{task}}
            </task>

            Workflow:
            1. Understand the requirements from the task description
            2. Explore the relevant code structure and conventions
            3. Identify all files that need to be modified or created
            4. Generate the complete code changes

            __TOOL_PRIORITIES__

            Important:
            - Do NOT apply any edits — only READ files
            - Output code in the specified format above
            - Your final response must include CODE_COMPLETE
            """.Replace("__TOOL_PRIORITIES__", BuildReadOnlyToolPriorities());

        return config;
    }
}
