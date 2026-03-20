using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace LLMClient.Agent.MiniSWE;

/// <summary>
/// 从 YAML 文件加载配置
/// </summary>
public static class MiniSweAgentConfigLoader
{
    /// <summary>
    /// 从 YAML 文件加载配置
    /// </summary>
    public static async Task<MiniSweAgentConfig> LoadFromYamlAsync(string yamlPath)
    {
        var yaml = await File.ReadAllTextAsync(yamlPath);
        return ParseYaml(yaml);
    }

    /// <summary>
    /// 解析 YAML 配置
    /// 注意：mini-swe-agent 使用嵌套结构，需要解析 agent 段和 model 段
    /// </summary>
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
    /// 从嵌入式资源加载默认配置
    /// </summary>
    public static MiniSweAgentConfig LoadDefaultToolCallConfig()
    {
        // 对应 mini.yaml 的配置
        return new MiniSweAgentConfig
        {
            SystemTemplate = """
                You are a helpful assistant that can interact with a computer.
                """,

            InstanceTemplate = """
                Please solve this issue: {{task}}

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
                """,

            UseToolCall = true,
            StepLimit = 0,
        };
    }

    /// <summary>
    /// 加载文本模式配置（用于不支持 ToolCall 的模型）
    /// </summary>
    public static MiniSweAgentConfig LoadDefaultTextBasedConfig()
    {
        // 对应 default.yaml 的配置
        return new MiniSweAgentConfig
        {
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
                """,

            InstanceTemplate = """
                Please solve this issue: {{task}}

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
                """,

            UseToolCall = false,
            StepLimit = 0,
        };
    }
}