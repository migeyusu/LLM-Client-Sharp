using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using LLMClient.Abstraction;
using LLMClient.Component.ViewModel.Base;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace LLMClient.Dialog
{
    /// <summary>
    /// OpenSpec /opsx: 命令解析 + Prompt 注入器
    /// 专为 Microsoft.Extensions.AI + 自定义 Agent 设计
    /// 
    /// 特性：
    /// - 只检查消息起始是否为 '/'（避免误判对话中的 opsx 单词）
    /// - 自动扫描 .github/prompts 目录加载所有 opsx-*.prompt.md
    /// - 支持 /opsx:apply 和 /opsx-apply 两种写法
    /// - 匹配成功后直接返回合并后的完整 Prompt（不操作 ChatHistory）
    /// </summary>
    public class OpenSpecPromptInjector : BaseViewModel, IPromptCommandAggregate
    {
        public string? ProjectRoot { get; }
        private readonly Dictionary<string, string> _commandPrompts = new(StringComparer.OrdinalIgnoreCase);
        private readonly string _promptsDirectory;

        private readonly Regex _commandRegex =
            new(@"^/opsx[:\-](\w+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>
        /// 获取当前已加载的所有可用命令列表（用于帮助提示或自动补全）
        /// </summary>
        public string[]? AvailableCommands
        {
            get;
            set
            {
                if (Equals(value, field)) return;
                field = value;
                OnPropertyChanged();
            }
        }

        public ICommand ReloadCommandsCommand { get; }

        /// <summary>
        /// 初始化并自动加载 .github/prompts 目录下的所有 opsx 命令
        /// </summary>
        /// <param name="projectRoot">项目根目录（默认当前目录）</param>
        public OpenSpecPromptInjector(string? projectRoot = null)
        {
            ProjectRoot = projectRoot;
            _promptsDirectory = Path.Combine(
                projectRoot ?? Directory.GetCurrentDirectory(), ".github", "prompts");
            LoadCommands();
            ReloadCommandsCommand = new RelayCommand(ReloadCommands);
        }

        /// <summary>
        /// 重新加载所有命令（支持运行时热更新）
        /// </summary>
        public void ReloadCommands()
        {
            _commandPrompts.Clear();
            LoadCommands();
        }

        private void LoadCommands()
        {
            if (!Directory.Exists(_promptsDirectory))
            {
                Trace.WriteLine($"[OpenSpec] Warning: Prompts directory not found: {_promptsDirectory}");
                return;
            }

            var files = Directory.GetFiles(_promptsDirectory, "opsx-*.prompt.md", SearchOption.TopDirectoryOnly);

            foreach (var filePath in files)
            {
                try
                {
                    var fileName = Path.GetFileNameWithoutExtension(filePath); // opsx-apply.prompt
                    //提取命令
                    var commandName = fileName
                        .Replace(".prompt", "", StringComparison.OrdinalIgnoreCase)
                        .Replace("opsx-", "", StringComparison.OrdinalIgnoreCase)
                        .Trim();
                    var rawContent = File.ReadAllText(filePath);
                    var cleanContent = StripFrontmatter(rawContent);
                    // 主命令名：apply
                    _commandPrompts[commandName] = cleanContent;
                    // 同时注册 opsx:apply 形式（方便用户输入）
                    _commandPrompts[$"opsx:{commandName}"] = cleanContent;
                    _commandPrompts[$"opsx-{commandName}"] = cleanContent;

                    Trace.WriteLine($"[OpenSpec] Loaded command: /{commandName} (from {Path.GetFileName(filePath)})");
                }
                catch (Exception ex)
                {
                    Trace.WriteLine($"[OpenSpec] Failed to load {filePath}: {ex.Message}");
                }
            }

            Trace.WriteLine($"[OpenSpec] Total commands loaded: {_commandPrompts.Count / 3} unique commands");

            AvailableCommands = _commandPrompts.Keys
                .Where(k => !k.Contains(':') && !k.Contains('-')) // 只返回干净的命令名
                .OrderBy(k => k)
                .ToArray();
        }

        /// <summary>
        /// 尝试检测并注入 OpenSpec 命令
        /// </summary>
        /// <param name="userInput">用户原始输入（例如 "/opsx:apply add-auth"）</param>
        /// <param name="ct"></param>
        /// <returns>
        /// 如果匹配成功，返回合并后的完整 Prompt（prompt内容 + 用户输入）<br/>
        /// 否则返回 null
        /// </returns>
        public async Task<string> TryGetInjectedPromptAsync(string userInput, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(userInput) || !userInput.StartsWith("/"))
                return userInput;

            // 只匹配起始的 /opsx:xxx 或 /opsx-xxx
            var match = _commandRegex.Match(userInput);
            if (!match.Success)
                return userInput;

            var commandKey = match.Groups[1].Value; // 例如 "apply"

            if (!_commandPrompts.TryGetValue(commandKey, out var promptTemplate))
                return userInput;

            // 合并策略（推荐）：把模板内容 + 用户原始输入拼接
            // 你可以根据需要改成更复杂的模板引擎（例如替换 {{user_input}}）
            var mergedPrompt = $"""
                                {promptTemplate.Trim()}

                                ---

                                **User Input**: {userInput}
                                """;

            return await Task.FromResult(mergedPrompt);
        }

        /// <summary>
        /// 判断用户输入是否为 OpenSpec 命令（仅检查起始字符）
        /// </summary>
        public bool IsOpenSpecCommand(string userInput)
        {
            return !string.IsNullOrWhiteSpace(userInput) &&
                   userInput.StartsWith("/") &&
                   _commandRegex.IsMatch(userInput);
        }

        /// <summary>
        /// 获取指定命令的原始 Prompt 模板（调试用）
        /// </summary>
        public string? GetRawPrompt(string commandName)
        {
            _commandPrompts.TryGetValue(commandName, out var content);
            return content;
        }

        /// <summary>
        /// 使用 YamlDotNet 移除 .prompt.md 文件开头的 YAML frontmatter
        /// 只保留实际的 Prompt 指令内容
        /// 
        /// 需要安装 NuGet 包：
        ///   dotnet add package YamlDotNet
        /// </summary>
        private static string StripFrontmatter(string content)
        {
            if (string.IsNullOrWhiteSpace(content) || !content.StartsWith("---"))
                return content?.Trim() ?? string.Empty;

            try
            {
                var deserializer = new DeserializerBuilder().Build();
                using var reader = new StringReader(content);

                // 反序列化第一个 YAML 文档（frontmatter），这会自动消耗掉整个 frontmatter
                _ = deserializer.Deserialize<object>(reader);

                // 此时 reader 已经位于第二个文档（或文件末尾）之前
                return reader.ReadToEnd().Trim();
            }
            catch
            {
                // 解析失败时使用简单回退方法
                return FallbackStripFrontmatter(content);
            }
        }

        /// <summary>
        /// 简单回退方法（当 YamlDotNet 解析失败时使用）
        /// </summary>
        private static string FallbackStripFrontmatter(string content)
        {
            var lines = content.Split('\n');
            int endIndex = Array.FindIndex(lines, 1, line => line.Trim() == "---");
            if (endIndex < 0) return content.Trim();
            return string.Join("\n", lines.Skip(endIndex + 1)).Trim();
        }
    }
}

/* ==================== 使用示例 ====================

// 1. 在 Program.cs 或 DI 容器中注册（推荐单例）
var injector = new OpenSpecPromptInjector(projectRoot: "/path/to/your/project");

// 2. 在你的聊天请求入口处使用（不操作 ChatHistory）

public async Task<ChatResponse> HandleUserMessageAsync(string userInput, ChatOptions options)
{
    string? injected = await injector.TryGetInjectedPromptAsync(userInput);

    if (injected != null)
    {
        // 匹配成功 → 直接用合并后的 Prompt 作为本次请求内容
        // 注意：这里完全不修改 messages 历史，符合你的要求
        var messages = new List<ChatMessage>
        {
            new ChatMessage(ChatRole.User, injected)
        };

        return await _chatClient.GetResponseAsync(messages, options);
    }
    else
    {
        // 普通对话 → 走原有逻辑
        return await _chatClient.GetResponseAsync(
            new[] { new ChatMessage(ChatRole.User, userInput) },
            options);
    }
}

// 3. 可选：在 UI 层显示可用命令
var commands = injector.GetAvailableCommands();
// 输出：apply, propose, archive, verify, continue ...

// 4. 支持运行时热更新（当你修改了 .prompt.md 文件后）
injector.ReloadCommands();

==================== 使用提示 ====================
- 建议把 .github/prompts 目录提交到 Git，这样团队所有人都能用
- 如果你想支持自定义 prompt 路径，可以扩展构造函数
- 合并策略可以改成更智能的版本（例如用 Handlebars 或简单字符串替换 {{input}}）
- 生产环境建议把 injector 做成 Scoped 或 Singleton 服务
*/