using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using LLMClient.Abstraction;
using LLMClient.Agent;
using LLMClient.Component;
using LLMClient.ContextEngineering;
using LLMClient.ContextEngineering.PromptGeneration;
using LLMClient.Dialog.Models;
using Microsoft.Extensions.AI;

namespace LLMClient.Dialog;

public class CodeBlockAnalysisResult
{
    [JsonPropertyName("blocks")] public List<CodeBlockLocation>? Blocks { get; set; }
}

public class CodeBlockLocation
{
    [JsonPropertyName("start")] public int StartLineIndex { get; set; }

    [JsonPropertyName("end")] public int EndLineIndex { get; set; }

    [JsonPropertyName("lang")] public string? Language { get; set; }
}

public class UserInputFormatter
{
    public static async Task<string?> FormatUserPromptAsync(ILLMChatClient client, RequestViewItem requestViewItem,
        string? systemPrompt = null, CancellationToken token = default)
    {
        var rawInput = requestViewItem.RawTextMessage;
        if (string.IsNullOrWhiteSpace(rawInput))
        {
            return null;
        }

        // 1. 快速过滤：太短的内容通常不需要处理
        if (string.IsNullOrWhiteSpace(rawInput) || rawInput.Length < 10) return rawInput;
        // 如果用户已经非常规范地使用了 ```，则跳过处理
        if (rawInput.Contains("```")) return rawInput;

        // 2. 预处理：行号标记
        var lines = rawInput.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        var sbInput = new StringBuilder();
        for (int i = 0; i < lines.Length; i++)
        {
            sbInput.AppendLine($"{i}|{lines[i]}"); // 使用 | 分隔行号更清晰
        }

        // 3. 构建 Prompt
        var prompt = """
                     You are a strict code extraction engine. Output JSON only.

                     # Goal
                     Analyze the provided text (lines starting with "row_number|") and identify specific multi-line code blocks.

                     # Context Hint
                     The user is likely discussing the following topics (use this to infer programming language if ambiguous): 
                     {{$contextHint}}

                     # Rules
                     1. **Multi-line Only**: Only mark code blocks that span multiple lines or are distinct standalone code snippets.
                     2. **Ignore Inline Code**: Do NOT mark single lines of code if they are embedded in a natural language sentence (e.g., "I used var x = 1 to test it" -> Ignore).
                     3. **Infer Language**: Accurately infer the programming language (e.g., "csharp", "python", "xml", "json"). If unsure, default to "text".
                     4. **JSON Structure**: You must return a JSON object with a key "blocks" containing an array of objects. Each object must have "start" (int), "end" (int), and "lang" (string).

                     # Example Input
                     0|I have a question about this code.
                     1|public void Test() {
                     2|   Console.WriteLine("Hello");
                     3|}
                     4|Can you explain `Console.WriteLine`?

                     # Example Output
                     { "blocks": [ { "start": 1, "end": 3, "lang": "csharp" } ] }

                     --------------------------------------------------
                     # User Input Analysis Table
                     {{$input}}
                     --------------------------------------------------
                     """;
        try
        {
            var message = await PromptTemplateRenderer.RenderAsync(prompt,
                new Dictionary<string, object?>
                {
                    { "contextHint", systemPrompt },
                    { "input", sbInput.ToString() }
                });
            var promptAgent = new PromptBasedAgent(client, TraceInvokeInteractor.Instance)
            {
                Timeout = TimeSpan.FromSeconds(30),
            };
            // 4. 配置 OpenAI JSON Mode
            var completedResult = await promptAgent.SendRequestAsync(new DialogContext([
                new RequestViewItem(message)
                {
                    ResponseFormat = ChatResponseFormat.ForJsonSchema<CodeBlockAnalysisResult>(),
                }
            ]), token);
            var jsonResponse = completedResult.FirstTextResponse;
            if (string.IsNullOrEmpty(jsonResponse))
            {
                return rawInput;
            }

            // 5. 反序列化
            var analysis = JsonSerializer.Deserialize<CodeBlockAnalysisResult>(jsonResponse);
            if (analysis?.Blocks?.Count is null or 0)
                return rawInput;
            // 6. 重组文本 
            return RebuildTextWithMarkdown(lines, analysis.Blocks);
        }
        catch (Exception ex)
        {
            // 日志记录异常 (建议集成 ILogger)
            Trace.WriteLine($"[SmartFormat Error]: {ex.Message}");
            return rawInput; // 降级处理：返回原文本
        }
    }

    private static string RebuildTextWithMarkdown(string[] lines, List<CodeBlockLocation> blocks)
    {
        var sb = new StringBuilder();
        // 排序并去重，防止 LLM 发癫返回重叠区间
        var sortedBlocks = blocks
            .OrderBy(b => b.StartLineIndex)
            .ToList();

        int currentLine = 0;
        int blockIndex = 0;

        while (currentLine < lines.Length)
        {
            if (blockIndex < sortedBlocks.Count && currentLine == sortedBlocks[blockIndex].StartLineIndex)
            {
                var block = sortedBlocks[blockIndex];

                // 安全性检查：防止结束行越界
                int endLine = Math.Min(block.EndLineIndex, lines.Length - 1);

                // 如果是无效块（start > end），跳过
                if (endLine < currentLine)
                {
                    blockIndex++;
                    continue;
                }

                sb.AppendLine($"```{block.Language}");
                for (int j = currentLine; j <= endLine; j++)
                {
                    sb.AppendLine(lines[j]);
                }

                sb.AppendLine("```");

                currentLine = endLine + 1;
                blockIndex++;
            }
            else
            {
                sb.AppendLine(lines[currentLine]);
                currentLine++;
            }
        }

        return sb.ToString().TrimEnd();
    }
}