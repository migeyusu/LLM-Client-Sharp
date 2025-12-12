using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;
using LLMClient.Abstraction;
using LLMClient.Component.Render;
using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel;
using FunctionCallContent = Microsoft.Extensions.AI.FunctionCallContent;
using FunctionResultContent = Microsoft.Extensions.AI.FunctionResultContent;
using TextContent = Microsoft.Extensions.AI.TextContent;

namespace LLMClient.Endpoints;

public enum FunctionCallEngineType
{
    OpenAI,
    Prompt
}

public abstract class FunctionCallEngine
{
    protected readonly KernelPluginCollection KernelPluginCollection;

    protected FunctionCallEngine(KernelPluginCollection kernelPluginCollection)
    {
        this.KernelPluginCollection = kernelPluginCollection;
    }

    public abstract void Initialize(ChatOptions options, ILLMModel model,
        IList<ChatMessage> chatMessages);

    public abstract bool TryParseFunctionCalls(ChatResponse response, out List<FunctionCallContent> functionCalls);

    public abstract void EncapsulateResult(ChatMessage replyMessage, IList<FunctionResultContent> results);

    public static FunctionCallEngine Create(FunctionCallEngineType engineType,
        KernelPluginCollection kernelPluginCollection)
    {
        return engineType switch
        {
            FunctionCallEngineType.Prompt => new PromptFunctionCallEngine(kernelPluginCollection),
            _ => new DefaultFunctionCallEngine(kernelPluginCollection)
        };
    }
}

public class DefaultFunctionCallEngine : FunctionCallEngine
{
    public DefaultFunctionCallEngine(KernelPluginCollection kernelPluginCollection) : base(kernelPluginCollection)
    {
    }

    public override void Initialize(ChatOptions options, ILLMModel model, IList<ChatMessage> chatMessages)
    {
        options.Tools = KernelPluginCollection.SelectMany(plugin => plugin).ToArray<AITool>();
    }

    public override bool TryParseFunctionCalls(ChatResponse response, out List<FunctionCallContent> functionCalls)
    {
        functionCalls = new List<FunctionCallContent>();
        foreach (var responseMessage in response.Messages)
        {
            foreach (var content in responseMessage.Contents)
            {
                if (content is FunctionCallContent functionCallContent)
                {
                    functionCalls.Add(functionCallContent);
                }
            }
        }

        return functionCalls.Count > 0;
    }

    public override void EncapsulateResult(ChatMessage replyMessage, IList<FunctionResultContent> results)
    {
        replyMessage.Role = ChatRole.Tool;
        for (var index = 0; index < results.Count; index++)
        {
            var functionResultContent = results[index];
            replyMessage.Contents.Insert(index, functionResultContent);
        }
    }
}

public class PromptFunctionCallEngine : FunctionCallEngine
{
    public PromptFunctionCallEngine(KernelPluginCollection kernelPluginCollection) : base(kernelPluginCollection)
    {
    }

    private string CreatePrompt(IEnumerable<AIFunction> functions)
    {
        var promptBuilder = new StringBuilder();
        promptBuilder.AppendLine(
            "In this environment you have access to a set of tools to help with answering, you can use them and wait for results.");
        promptBuilder.AppendLine("");
        promptBuilder.AppendLine("## Tool Definition Formatting");
        promptBuilder.AppendLine("");
        promptBuilder.AppendLine(
            "Tools are formatted within several <tool></tool> XML tags.:");
        promptBuilder.AppendLine("<tools>");
        promptBuilder.AppendLine("  <tool>");
        promptBuilder.AppendLine("    <name>{tool1_name}</name>");
        promptBuilder.AppendLine("    <description>{tool1_description}</description>");
        promptBuilder.AppendLine("    <parameters>{argument_json_scheme}</parameters>");
        promptBuilder.AppendLine("    <returns>{return_json_schema}</returns>");
        promptBuilder.AppendLine("  </tool>");
        promptBuilder.AppendLine("  <tool>");
        promptBuilder.AppendLine("    <name>{tool2_name}</name>");
        promptBuilder.AppendLine("    <description>{tool2_description}</description>");
        promptBuilder.AppendLine("    <parameters>{argument_json_scheme}</parameters>");
        promptBuilder.AppendLine("    <returns>{return_json_schema}</returns>");
        promptBuilder.AppendLine("  </tool>");
        promptBuilder.AppendLine("</tools>");
        promptBuilder.AppendLine(
            "As previous shown, each tool has a name, a description, and a json schema for its parameters and return values. <tool> tags are enclosed within <tools> tags.");
        promptBuilder.AppendLine("");
        promptBuilder.AppendLine("## Tool Call Request Formatting");
        promptBuilder.AppendLine("You can use tools by listing in message by following format:");
        promptBuilder.AppendLine("");
        promptBuilder.AppendLine("<tool_calls>");
        promptBuilder.AppendLine("  <tool_call>");
        promptBuilder.AppendLine("      <name>{tool1_name}</name>");
        promptBuilder.AppendLine("      <arguments>{arguments-json-object}</arguments>");
        promptBuilder.AppendLine("  </tool_call>");
        promptBuilder.AppendLine("  <tool_call>");
        promptBuilder.AppendLine("      <name>{tool2_name}</name>");
        promptBuilder.AppendLine("      <arguments>{arguments-json-object}</arguments>");
        promptBuilder.AppendLine("  </tool_call>");
        promptBuilder.AppendLine("</tool_calls>");
        promptBuilder.AppendLine("");
        promptBuilder.AppendLine(
            "As previous shown, tool name and arguments within <tool_call></tool_call> XML tags,and the arguments should be a JSON object containing the parameters required by that tool.");
        promptBuilder.AppendLine("For example(for only one tool call):");
        promptBuilder.AppendLine("<tool_calls>");
        promptBuilder.AppendLine("  <tool_call>");
        promptBuilder.AppendLine("      <name>python_interpreter</name>");
        promptBuilder.AppendLine("      <arguments>{\"code\": \"5 + 3 + 1294.678\"}</arguments>");
        promptBuilder.AppendLine("  </tool_call>");
        promptBuilder.AppendLine("</tool_calls>");
        promptBuilder.AppendLine("");
        promptBuilder.AppendLine(
            "'python_interpreter' is the tool name, and '{\"code\": \"5 + 3 + 1294.678\"}' is the JSON object representing the arguments for that tool.");
        promptBuilder.AppendLine("");
        promptBuilder.AppendLine("## Tool Call Result Formatting");
        promptBuilder.AppendLine(
            "After you output the tool call request, environment will call the tool(s), and Then you are provided the result of that tool call in the user's request which should be formatted as follows:");
        promptBuilder.AppendLine("");
        promptBuilder.AppendLine("<tool_call_results>");
        promptBuilder.AppendLine("  <tool_call_result>");
        promptBuilder.AppendLine("      <name>{tool_name}</name>");
        promptBuilder.AppendLine("      <result>{result-json-object}</result>");
        promptBuilder.AppendLine("  </tool_call_result>");
        promptBuilder.AppendLine("</tool_call_results>");
        promptBuilder.AppendLine("");
        promptBuilder.AppendLine(
            "The result are formatted within <tool_call_result></tool_call_result> XML tags, which can represent a file or any other output type. You can use this result as input for the next action.");
        promptBuilder.AppendLine(
            "Warning: **you can and only can send requests of tools calling, but not imagine results by yourself!**");
        promptBuilder.AppendLine("");
        promptBuilder.AppendLine("## Tool Use Examples");
        promptBuilder.AppendLine("");
        promptBuilder.AppendLine("Here are a few examples using notional tools:");
        promptBuilder.AppendLine("---");
        promptBuilder.AppendLine("User: \"What is the result of the following operation: 5 + 3 + 1294.678?\"");
        promptBuilder.AppendLine("");
        promptBuilder.AppendLine(
            "Assistant: I can use the python_interpreter tool to calculate the result of the operation.");
        promptBuilder.AppendLine("<tool_calls>");
        promptBuilder.AppendLine("  <tool_call>");
        promptBuilder.AppendLine("      <name>python_interpreter</name>");
        promptBuilder.AppendLine("      <arguments>{\"code\": \"5 + 3 + 1294.678\"}</arguments>");
        promptBuilder.AppendLine("  </tool_call>");
        promptBuilder.AppendLine("</tool_calls>");
        promptBuilder.AppendLine("");
        promptBuilder.AppendLine("User:");
        promptBuilder.AppendLine("<tool_call_results>");
        promptBuilder.AppendLine("  <tool_call_result>");
        promptBuilder.AppendLine("      <name>python_interpreter</name>");
        promptBuilder.AppendLine("      <result>1302.678</result>");
        promptBuilder.AppendLine("  </tool_call_result>");
        promptBuilder.AppendLine("</tool_call_results>");
        promptBuilder.AppendLine("");
        promptBuilder.AppendLine("Assistant: The result of the operation is 1302.678.");
        promptBuilder.AppendLine("");
        promptBuilder.AppendLine("---");
        promptBuilder.AppendLine("User: \"Which city has the highest population , Guangzhou or Shanghai?\"");
        promptBuilder.AppendLine("");
        promptBuilder.AppendLine("Assistant: I can use the search tool to find the population of Guangzhou.");
        promptBuilder.AppendLine("<tool_calls>");
        promptBuilder.AppendLine("  <tool_call>");
        promptBuilder.AppendLine("      <name>search</name>");
        promptBuilder.AppendLine("      <arguments>{\"query\": \"Population Guangzhou\"}</arguments>");
        promptBuilder.AppendLine("  </tool_call>");
        promptBuilder.AppendLine("</tool_calls>");
        promptBuilder.AppendLine("");
        promptBuilder.AppendLine("User:");
        promptBuilder.AppendLine("<tool_call_results>");
        promptBuilder.AppendLine("  <tool_call_result>");
        promptBuilder.AppendLine("      <name>search</name>");
        promptBuilder.AppendLine(
            "      <result>Guangzhou has a population of 15 million inhabitants as of 2021.</result>");
        promptBuilder.AppendLine("  </tool_call_result>");
        promptBuilder.AppendLine("</tool_call_results>");
        promptBuilder.AppendLine("");
        promptBuilder.AppendLine("Assistant: I can use the search tool to find the population of Shanghai.");
        promptBuilder.AppendLine("<tool_calls>");
        promptBuilder.AppendLine("  <tool_call>");
        promptBuilder.AppendLine("      <name>search</name>");
        promptBuilder.AppendLine("      <arguments>{\"query\": \"Population Shanghai\"}</arguments>");
        promptBuilder.AppendLine("  </tool_call>");
        promptBuilder.AppendLine("</tool_calls>");
        promptBuilder.AppendLine("");
        promptBuilder.AppendLine("User:");
        promptBuilder.AppendLine("<tool_call_results>");
        promptBuilder.AppendLine("  <tool_call_result>");
        promptBuilder.AppendLine("      <name>search</name>");
        promptBuilder.AppendLine("      <result>26 million (2019)</result>");
        promptBuilder.AppendLine("  </tool_call_result>");
        promptBuilder.AppendLine("</tool_call_results>");
        promptBuilder.AppendLine("");
        promptBuilder.AppendLine(
            "Assistant: The population of Shanghai is 26 million, while Guangzhou has a population of 15 million. Therefore, Shanghai has the highest population.");
        promptBuilder.AppendLine("");
        promptBuilder.AppendLine("");
        promptBuilder.AppendLine("## Tool Use Available Tools");
        promptBuilder.AppendLine("Above example were using notional tools that might not exist for you.");
        promptBuilder.AppendLine("NOW you only have access to these tools:");
        promptBuilder.AppendLine("<tools>");
        foreach (var func in functions)
        {
            promptBuilder.AppendLine("  <tool>");
            promptBuilder.AppendLine($"    <name>{func.Name}</name>");
            promptBuilder.AppendLine($"    <description>{func.Description}</description>");
            promptBuilder.AppendLine($"    <parameters>{func.JsonSchema.ToString()}</parameters>");
            promptBuilder.AppendLine($"    <returns>{func.ReturnJsonSchema?.ToString()}</returns>");
            promptBuilder.AppendLine("  </tool>");
        }

        promptBuilder.AppendLine("</tools>");
        return promptBuilder.ToString();
    }

    private IEnumerable<FunctionCallContent> ParseResponseAsync(string llmResponse)
    {
        var match = Regex.Match(llmResponse, @"<tool_calls>\s*(.*?)\s*</tool_calls>", RegexOptions.Singleline);
        if (!match.Success)
        {
            return [];
        }

        var toolCallContent = match.Groups[1].Value.Trim();
        // 使用XML解析工具调用内容
        var functionCalls = new List<FunctionCallContent>();
        try
        {
            var toolCallXml = XElement.Parse($"<root>{toolCallContent}</root>");
            var toolCall = toolCallXml.Element("tool_call");
            if (toolCall != null)
            {
                string? name = toolCall.Element("name")?.Value;
                string? arguments = toolCall.Element("arguments")?.Value;
                if (!string.IsNullOrWhiteSpace(name) && arguments != null)
                {
                    var argumentsDictionary =
                        System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object?>>(arguments);
                    functionCalls.Add(new FunctionCallContent(name, name, argumentsDictionary));
                }
            }
        }
        catch (System.Xml.XmlException exception)
        {
            throw new Exception("Error xml format: " + exception.Message);
        }

        return functionCalls;
    }

    public override void Initialize(ChatOptions options, ILLMModel model, IList<ChatMessage> chatMessages)
    {
        var prompt = CreatePrompt(KernelPluginCollection.SelectMany(plugin => plugin));
        if (model.SupportSystemPrompt)
        {
            var systemChatMessage = chatMessages.FirstOrDefault(message => message.Role == ChatRole.System);
            if (systemChatMessage == null)
            {
                systemChatMessage = new ChatMessage(ChatRole.System, "");
                chatMessages.Insert(0, systemChatMessage);
            }

            var text = ((TextContent)systemChatMessage.Contents[0]).Text;
            systemChatMessage.Contents[0] =
                new TextContent(string.IsNullOrEmpty(text) ? prompt : text + "\n" + prompt);
        }
    }

    public override bool TryParseFunctionCalls(ChatResponse response, out List<FunctionCallContent> functionCalls)
    {
        var content = response.Messages.FirstOrDefault()?.Contents.FirstOrDefault();
        if (content is TextContent textContent)
        {
            functionCalls = ParseResponseAsync(textContent.Text).ToList();
            return functionCalls.Count > 0;
        }

        functionCalls = [];
        return false;
    }

    public override void EncapsulateResult(ChatMessage replyMessage, IList<FunctionResultContent> results)
    {
        replyMessage.Role = ChatRole.User;
        var container = new ToolCallResultsContainer
        {
            ToolCalls = results.Select(r =>
            {
                string resultString = r.Result switch
                {
                    null => "null",
                    string s => s,
                    _ => JsonSerializer.Serialize(r.Result)
                };

                return new ToolCallResultElement
                {
                    Name = r.CallId,
                    ResultContent = resultString
                };
            }).ToList()
        };

        // 2. 用 XmlSerializer 把对象序列化为字符串
        var serializer = new XmlSerializer(typeof(ToolCallResultsContainer));
        var sb = new StringBuilder();
        using (var sw = new StringWriter(sb))
        {
            using (XmlWriter xmlWriter = XmlWriter.Create(sw, new XmlWriterSettings() { Indent = true }))
            {
                serializer.Serialize(xmlWriter, container);
            }
        }
        
        // 添加到回复消息中
        replyMessage.Contents.Insert(0, new TextContent(sb.ToString()));
    }
}