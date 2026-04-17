using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace LLMClient.Agent.MiniSWE;

/// <summary>
/// Load Mini-SWE / Windows-Agent config from YAML or defaults
/// </summary>
public static class MiniSweAgentConfigLoader
{
    public static async Task<MiniSweAgentConfig> LoadFromYamlAsync(string yamlPath)
    {
        var yaml = await File.ReadAllTextAsync(yamlPath);
        return ParseYaml(yaml);
    }

    public static MiniSweAgentConfig ParseYaml(string yaml)
    {
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .Build();

        var yamlConfig = deserializer.Deserialize<Dictionary<string, object>>(yaml);
        var config = new MiniSweAgentConfig();

        if (yamlConfig.TryGetValue("agent", out var agentObj) && agentObj is Dictionary<object, object> agentDict)
        {
            if (agentDict.TryGetValue("system_template", out var sysTemplate))
            {
                config.SystemTemplate = sysTemplate?.ToString() ?? config.SystemTemplate;
            }

            if (agentDict.TryGetValue("instance_template", out var instTemplate))
            {
                config.InstanceTemplate = instTemplate?.ToString() ?? config.InstanceTemplate;
            }

            if (agentDict.TryGetValue("step_limit", out var stepLimit))
            {
                config.StepLimit = Convert.ToInt32(stepLimit);
            }

            if (agentDict.TryGetValue("platform_id", out var platformId))
            {
                if (Enum.TryParse<AgentPlatform>(platformId?.ToString(), true, out var pid))
                {
                    config.PlatformId = pid;
                }
            }

            if (agentDict.TryGetValue("include_tool_instructions", out var includeToolInstructions))
            {
                config.IncludeToolInstructions = Convert.ToBoolean(includeToolInstructions);
            }

            if (agentDict.TryGetValue("include_rag_instructions", out var includeRagInstructions))
            {
                config.IncludeRagInstructions = Convert.ToBoolean(includeRagInstructions);
            }

            if (agentDict.TryGetValue("wsl_distribution_name", out var wslDistro))
            {
                config.WslDistributionName = wslDistro?.ToString() ?? config.WslDistributionName;
            }

            if (agentDict.TryGetValue("wsl_user_name", out var wslUser))
            {
                config.WslUserName = wslUser?.ToString() ?? config.WslUserName;
            }

            if (agentDict.TryGetValue("map_working_directory_to_wsl", out var mapWsl))
            {
                config.MapWorkingDirectoryToWsl = Convert.ToBoolean(mapWsl);
            }
        }

        if (yamlConfig.TryGetValue("model", out var modelObj) && modelObj is Dictionary<object, object> modelDict)
        {
            if (modelDict.TryGetValue("observation_template", out var obsTemplate))
            {
                config.ObservationTemplate = obsTemplate?.ToString() ?? config.ObservationTemplate;
            }

            if (modelDict.TryGetValue("format_error_template", out var formatErrorTemplate))
            {
                config.FormatErrorTemplate = formatErrorTemplate?.ToString() ?? config.FormatErrorTemplate;
            }
        }

        return config;
    }

    /// <summary>
    /// Linux mini-swe style, tool-call mode, as close to original as practical.
    /// </summary>
    public static MiniSweAgentConfig LoadDefaultLinuxToolCallConfig()
    {
        return new MiniSweAgentConfig
        {
            PlatformId = AgentPlatform.Linux,
            IncludeToolInstructions = false,
            IncludeRagInstructions = false,
            UseToolCall = true,
            ToolName = "bash",
            StepLimit = 0,
            SystemTemplate = """
                You are a helpful assistant that can interact with a computer.
                
                {{{project_information}}}
                """,
            InstanceTemplate = """
                Please solve this issue:
                
                <issue>
                {{task}}
                </issue>

                You can execute bash commands and edit files to implement the necessary changes.

                ## Recommended Workflow

                This workflow should be done step-by-step so that you can iterate on your changes and any possible problems.

                1. Analyze the codebase by finding and reading relevant files
                2. Create a script to reproduce the issue
                3. Edit the source code to resolve the issue
                4. Verify your fix works by running your script again
                5. Test edge cases to ensure your fix is robust
                6. Submit your changes and finish your work by issuing the following command: `echo COMPLETE_TASK_AND_SUBMIT_FINAL_OUTPUT`.
                   Do not combine it with any other command. <important>After this command, you cannot continue working on this task.</important>

                ## Command Execution Rules

                You are operating in an environment where

                1. You issue at least one command
                2. The system executes the command(s) in a subshell
                3. You see the result(s)
                4. You write your next command(s)

                <system_information>
                {{system}} {{release}} {{version}} {{machine}}
                </system_information>
                """,
            ObservationTemplate = """
                {{"{{"}}#if output.exception_info{{"}}"}}
                <exception>{{"{{"}}output.exception_info{{"}}"}}</exception>
                {{"{{"}}/if{{"}}"}}
                <returncode>{{"{{"}}output.returncode{{"}}"}}</returncode>
                {{"{{"}}#if (lt output.output.length 10000){{"}}"}}
                <output>
                {{"{{"}}output.output{{"}}"}}
                </output>
                {{"{{"}}else{{"}}"}}
                <warning>
                The output of your last command was too long.
                Please try a different command that produces less output.
                </warning>
                <output_head>
                {{"{{"}}output.output.[0..5000]{{"}}"}}
                </output_head>
                <elided_chars>
                {{"{{"}}sub output.output.length 10000{{"}}"}} characters elided
                </elided_chars>
                <output_tail>
                {{"{{"}}output.output.[output.output.length..-5000]{{"}}"}}
                </output_tail>
                {{"{{"}}/if{{"}}"}}
                """,
            FormatErrorTemplate = """
                Tool call error:

                <error>
                {{"{{"}}error{{"}}"}}
                </error>

                Here is general guidance on how to submit correct toolcalls:

                Every response needs to use the 'bash' tool at least once to execute commands.

                Call the bash tool with your command as the argument:
                - Tool: bash
                - Arguments: {"command": "your_command_here"}
                """
        };
    }

    /// <summary>
    /// Linux mini-swe style, text-based mode for models without native tool-call.
    /// </summary>
    public static MiniSweAgentConfig LoadDefaultLinuxTextBasedConfig()
    {
        return new MiniSweAgentConfig
        {
            PlatformId = AgentPlatform.Linux,
            IncludeToolInstructions = false,
            IncludeRagInstructions = false,
            UseToolCall = false,
            ToolName = "bash",
            StepLimit = 0,
            SystemTemplate = """
                You are a helpful assistant that can interact with a computer.

                Your response must contain exactly ONE bash code block with ONE command (or commands connected with && or ||).
                Include a THOUGHT section before your command where you explain your reasoning process.
                Format your response as shown in <format_example>.

                <format_example>
                Your reasoning and analysis here. Explain why you want to perform the action.

                ```mswea_bash_command
                your_command_here
                ```
                </format_example>

                Failure to follow these rules will cause your response to be rejected.
                
                {{{project_information}}}
                """,
            InstanceTemplate = """
                Please solve this issue:
                
                <issue>
                {{task}}
                </issue>

                You can execute bash commands and edit files to implement the necessary changes.

                ## Recommended Workflow

                This workflow should be done step-by-step so that you can iterate on your changes and any possible problems.

                1. Analyze the codebase by finding and reading relevant files
                2. Create a script to reproduce the issue
                3. Edit the source code to resolve the issue
                4. Verify your fix works by running your script again
                5. Test edge cases to ensure your fix is robust
                6. Submit your changes and finish your work by issuing the following command: `echo COMPLETE_TASK_AND_SUBMIT_FINAL_OUTPUT`.
                   Do not combine it with any other command. <important>After this command, you cannot continue working on this task.</important>

                ## Important Rules

                1. Every response must contain exactly one action
                2. The action must be enclosed in triple backticks
                3. Directory or environment variable changes are not persistent. Every action is executed in a new subshell.

                <system_information>
                {{system}} {{release}} {{version}} {{machine}}
                </system_information>
                """,
            ObservationTemplate = """
                {{"{{"}}#if output.exception_info{{"}}"}}
                <exception>{{"{{"}}output.exception_info{{"}}"}}</exception>
                {{"{{"}}/if{{"}}"}}
                <returncode>{{"{{"}}output.returncode{{"}}"}}</returncode>
                {{"{{"}}#if (lt output.output.length 10000){{"}}"}}
                <output>
                {{"{{"}}output.output{{"}}"}}
                </output>
                {{"{{"}}else{{"}}"}}
                <warning>
                The output of your last command was too long.
                </warning>
                <output_head>
                {{"{{"}}output.output.[0..5000]{{"}}"}}
                </output_head>
                <output_tail>
                {{"{{"}}output.output.SUBSTRING output.output.length 5000{{"}}"}}
                </output_tail>
                {{"{{"}}/if{{"}}"}}
                """,
            FormatErrorTemplate = """
                Format error:

                <error>
                {{"{{"}}error{{"}}"}}
                </error>

                Here is general guidance on how to format your response:

                Please always provide EXACTLY ONE action in triple backticks.
                If you want to end the task, please issue the following command: `echo COMPLETE_TASK_AND_SUBMIT_FINAL_OUTPUT`
                without any other command.

                <response_example>
                Here are some thoughts about why you want to perform the action.

                ```mswea_bash_command
                <action>
                ```
                </response_example>
                """
        };
    }

    /// <summary>
    /// Windows-first agent config:
    /// windows.yaml main body + structured platform/tool/rag injections.
    /// </summary>
    public static MiniSweAgentConfig LoadDefaultWindowsConfig()
    {
        return new MiniSweAgentConfig
        {
            PlatformId = AgentPlatform.Windows,
            IncludeToolInstructions = true,
            IncludeRagInstructions = true,
            UseToolCall = true,
            ToolName = "toolcall",
            StepLimit = 0,
            SystemTemplate = """
                You are a helpful assistant that can interact with development tools on a Windows system.

                Your job is to solve software engineering tasks by reasoning step by step, inspecting files, editing code, and running commands when needed.

                You must work incrementally:
                - inspect the relevant files and project structure
                - make focused edits
                - verify the result
                - iterate if necessary

                Prefer structured tool usage over ad-hoc shell editing whenever possible.

                Tool usage policy:
                - Prefer the FileSystem tool group for reading files, inspecting code, previewing edits, and applying edits.
                - Prefer the WinCLI tool group for running builds, tests, scripts, searches, and other Windows command-line tasks.
                - Do not assume a bash shell is available.
                - Prefer PowerShell-style commands on Windows.

                Response policy:
                - Always explain your reasoning briefly before taking an action.
                - Take one coherent step at a time.
                - Avoid unnecessary repetition.
                - Do not claim a file was changed unless the edit tool was successfully applied.
                - Do not claim a fix works unless you actually verified it.
                - Your final response must include COMPLETE_TASK_AND_SUBMIT_FINAL_OUTPUT.

                When reading code with line numbers:
                - use the line numbers for inspection and reference
                - do not include line number prefixes in file edit operations

                Failure to follow the tool usage and editing rules may cause your response to be rejected.

                {{{project_information}}}
                
                {{{platform_instructions}}}

                {{{tool_instructions}}}

                {{{rag_instructions}}}
                """,
            InstanceTemplate = """
                Please solve this issue:
                
                <issue>
                {{task}}
                </issue>

                You are working in a Windows-oriented development environment.

                Available capabilities may include:
                - structured file reading and editing tools
                - Windows command execution through PowerShell or CMD
                - .NET SDK and related build/test tools
                - platform-specific tooling depending on the project

                ## Recommended Workflow

                Work step by step so you can inspect, edit, verify, and recover safely.

                1. Understand the project structure and identify the relevant files
                2. Inspect the relevant source code in focused regions
                3. If needed, reproduce the issue or understand the expected behavior
                4. Edit the source code using structured file edit tools when possible
                5. Preview edits before applying them
                6. Verify the fix by running an appropriate build, test, or reproduction command
                7. Re-check the edited code if necessary
                8. Finish only when the result has been verified as much as possible then output COMPLETE_TASK_AND_SUBMIT_FINAL_OUTPUT or 'echo COMPLETE_TASK_AND_SUBMIT_FINAL_OUTPUT' to indicate completion

                {{{tool_selection_guidance}}}

                ## Windows-Specific Guidance

                - Prefer PowerShell syntax and Windows-native commands
                - Do not assume bash, sed, awk, grep, ls, cat, or nl are available
                - If a command depends on a directory, specify it explicitly
                - Be careful with spaces in Windows paths; quote them when necessary
                - Prefer non-interactive commands
                - File edits may require user confirmation in the UI before they are applied

                ## Editing Rules

                - Inspect the relevant file region before editing
                - Use line-numbered reads when precise inspection is useful
                - Do not use line numbers as the edit key
                - In each edit operation, oldText must include enough surrounding context to uniquely match one location
                - Use PreviewEditAsync before ApplyEditAsync whenever editing intent is not yet fully certain

                ## Verification Rules

                - After editing, run an appropriate verification step whenever possible
                - For .NET projects, prefer commands such as `dotnet build`, `dotnet test`, or targeted project commands
                - For C++/Qt projects, use the project’s actual configured build/test workflow if available
                - If verification cannot be completed, explain why clearly

                <system_information>
                {{system}} {{release}} {{version}} {{machine}}
                </system_information>

                ## Response Expectations

                For each step:
                1. Briefly explain what you are trying to do
                2. Use the appropriate tool(s) to perform that step
                3. Observe the result before deciding the next step

                Example good behavior:
                - inspect a target file range with line numbers
                - preview a focused edit
                - apply the edit
                - run a build or test command
                """,
            ObservationTemplate = """
                {{#if output.exception_info}}
                <exception>{{output.exception_info}}</exception>
                {{/if}}
                <returncode>{{output.returncode}}</returncode>
                {{#if output.output}}
                <output>
                {{output.output}}
                </output>
                {{/if}}
                """,
            FormatErrorTemplate = """
                Tool usage error:

                <error>
                {{error}}
                </error>

                General guidance:
                - Use the available tools directly when you need to inspect files, edit files, or run commands.
                - Do not describe an action without actually calling the relevant tool.
                - Prefer FileSystem tools for reading and editing files.
                - Prefer WinCLI for Windows command execution.
                - When editing files, inspect first, preview edits, then apply edits.

                If the task is complete, clearly explain what was changed and how it was verified.
                """
        };
    }

    /// <summary>
    /// Default WSL config (Linux tool-call style)
    /// </summary>
    public static MiniSweAgentConfig LoadDefaultWslConfig()
    {
        var config = LoadDefaultLinuxToolCallConfig();
        config.PlatformId = AgentPlatform.Wsl;
        config.SystemTemplate = """
            You are a helpful assistant that can interact with a computer via WSL (Windows Subsystem for Linux).
            
            {{{project_information}}}
            """;
        // WSL specific adjustments can be added here if needed
        return config;
    }
}

