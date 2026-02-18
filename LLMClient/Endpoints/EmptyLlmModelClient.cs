using LLMClient.Abstraction;
using LLMClient.Component;
using LLMClient.Endpoints.OpenAIAPI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;

namespace LLMClient.Endpoints;

public class StubLlmClient : ILLMChatClient
{
    public IEndpointModel Model { get; } = StubLLMChatModel.Instance;
    public IModelParams Parameters { get; set; } = new DefaultModelParam();
    public string Name { get; } = "StubLlmClient";
    public bool IsResponding { get; set; }

    public async Task<CompletedResult> SendRequest(DialogContext context, IInvokeInteractor? interactor = null,
        CancellationToken cancellationToken = default)
    {
#if DEBUG
        interactor ??= new DebugInvokeInteractor();
#endif
        var str =
            "下面给出一套“面向你定义的 Workflow/Agent 流程”的工具集清单（只定义能力与职责边界，不涉及任何代码）。原则是：**工具按 Agent 职责分组**，并且尽量让工具返回**结构化结果（JSON）**、可审计、可缓存、可增量。\n\n> 说明：一些工具会被多个 Agent 共用（例如读文件、搜索符号）。我仍按“主使用者”归类，并标注“共享/可复用”。\n\n---\n\n## 0. 基础设施层（所有 Agent 共用）\n这些是“跑任何 workflow 都必须有”的底座能力。\n\n### Workspace / 文件系统\n- `workspace.get_root()`：工作区根目录、解决方案入口\n- `fs.list(path, depth, include, exclude, maxEntries)`：列文件/目录（你已有，但建议支持过滤与深度）\n- `fs.stat(path)`：大小、修改时间、是否文本、行数（可选）\n- `fs.read_text(path, range?, maxBytes?, maxTokens?)`：支持行范围/截断回传\n- `fs.write_text(path, content, mode)`：覆盖/追加（通常给 Coder）\n- `fs.apply_patch(path, unifiedDiff | edits)`：补丁式修改（强烈建议）\n- `fs.search_text(query, scope, fileGlobs, regex, topK)`：纯文本快速搜索（grep）\n- `fs.delete(path)` / `fs.move(src,dst)` / `fs.copy(src,dst)`\n\n### 进程与命令（建议强权限控制）\n- `proc.run(command, args, cwd, timeout, env, capture)`：执行构建/测试/格式化等  \n  *建议带 allowlist 与沙箱策略；输出要可截断并带 exitCode。*\n\n### 变更与差异\n- `diff.preview(path)`：工作区文件与磁盘/索引的差异\n- `diff.compare(a,b)`：任意两段文本差异\n- `changes.list()`：列出未提交变更、受影响文件\n\n### Token/上下文预算与裁剪（供 StepDispatcher/Inspector 使用）\n- `context.estimate_tokens(text|paths|snippets)`：估算 tokens\n- `context.pack(snippets, budget, strategy)`：按预算打包/裁剪片段（很关键）\n\n---\n\n## 1. Inspector（分析项目结构 / 上下文工程）\n目标：**快速回答“这是什么项目、怎么构建、关键入口在哪、有哪些模块”**，产出后续可用的“工程画像”。\n\n### 项目识别与元数据\n- `project.detect()`：识别语言/框架/解决方案入口（.sln/.csproj 等）\n- `project.list_solutions()` / `project.list_projects(solution)`：枚举\n- `project.get_project_info(project)`：TargetFramework、PackageReferences、ProjectReferences 等\n\n### 静态分析（结构级，不要拉全量实现体）\n- `code.index.build(scope, mode)`：构建索引（符号/文件/依赖图），mode=fast/full\n- `code.index.status()`：索引是否最新、覆盖范围\n- `code.symbol.list(topK, filters)`：列出重要类型/入口（namespace/type/signature/location）\n- `code.deps.graph(module?)`：项目引用图/程序集依赖图\n- `code.entrypoints.detect()`：主程序入口、Web 路由入口、DI 注册点（可做启发式）\n\n### Map / 摘要产出（供 Prompt 注入）\n- `map.file_tree(compact, depth, excludeRules)`：文件树（你已有实现方向）\n- `map.logical(topKTypes, includeMembersLevel, filters)`：命名空间/类型/成员签名级摘要（不含实现体）\n\n---\n\n## 2. TaskGenerator（理解需求 + 补全信息 + 触发进一步探测）\n目标：把用户需求转成“可规划的任务”，并在规划前**主动补齐缺失信息**。\n\n### 需求澄清与约束收集（偏“环境/配置”）\n- `requirements.collect(templateId)`：按模板问询（例如“重构/迁移/加功能/修bug”不同问法）\n- `constraints.get()`：预算、时间、可用工具、是否允许改 API、兼容性要求（可以来自用户设置）\n\n### 定向信息抓取（为 Planner 做准备）\n- `code.search_symbols(query, topK, filters)`：符号名/注释搜索（结构化）\n- `code.search_text(query, scope, topK)`：文本搜索（快速定位）\n- `code.references.find(symbolId | location)`：找引用（Roslyn）\n- `code.callgraph.get(symbolId, depth)`：调用关系（可选，重但很值）\n\n---\n\n## 3. SchemeRouter（只负责路由架构阶段）\n目标：判断走“简单流程/复杂流程/多 architect 并行”。\n\n### 评估信号工具（用于量化困难度）\n- `complexity.estimate(taskSpec, projectStats)`：给出复杂度分数+理由  \n  输入可包含：变更范围、影响面、测试缺失、索引覆盖率、项目大小等\n- `project.stats()`：文件数、语言、解决方案规模、近似 token map 成本、是否有测试等\n\n> Router 本身最好不需要更多工具，更多是消费 Inspector/TaskGenerator 的结果。\n\n---\n\n## 4. Architect（生成候选架构/uml/方案）\n目标：在“方案空间”内探索，而不是改代码。需要的工具偏“读索引/读关键文件/生成图”。\n\n### 架构理解与建模\n- `code.modules.detect()`：按命名空间/目录/项目引用聚类模块\n- `code.diagram.render(type, data)`：生成 mermaid/plantuml 文本（不必生成图片也可）\n- `code.policy.check(architectureRules)`：例如分层约束、禁止引用（可选）\n\n### 关键点抽样读取（避免全量读）\n- `fs.read_text(path, range, maxTokens)`：读取关键入口文件片段（共享）\n- `code.symbol.get(symbolId)`：符号摘要/签名/注释/位置（共享）\n- `code.project.get_config()`：如 DI/路由/Startup/Program.cs 关键配置定位（可做聚合工具）\n\n---\n\n## 5. Refiner（多 Architect 方案选择与精炼）\n目标：对多个架构候选进行对比、合并、风险评估，选出一套可落地的方案。\n\n### 方案对比与评估\n- `plan.compare(candidates, criteria)`：可行性/复杂度/风险/收益评分\n- `risk.analyze(plan, projectFacts)`：输出风险清单（breaking change、性能、兼容性）\n- `impact.estimate(plan)`：影响文件数、模块数、API 变更面（基于索引粗估）\n\n---\n\n## 6. Planner（生成明确 Step 列表）\n目标：把方案转为**可执行步骤**（每步输入、输出、验收标准、所需工具、回滚点）。\n\n### 计划结构化与校验\n- `plan.validate(steps, toolAvailability, constraints)`：检查每步是否可执行、工具是否齐\n- `plan.expand(step)`：把抽象步骤展开为更细粒度子步骤（可选）\n- `acceptance.template(kind)`：生成验收清单模板（功能/重构/性能/安全）\n\n### 依赖与顺序\n- `dependency.order(steps)`：排序/并行建议（你只允许 planning 并行，执行顺序化）\n\n---\n\n## 7. StepDispatcher（调度/循环/状态机）\n目标：执行期的“大脑”：按步骤发给 Coder/Reviewer，处理失败重试/中断/继续。\n\n### 状态与记忆（建议强结构化存储）\n- `run.create(workflowId, plan)`：创建一次运行实例\n- `run.get_status(runId)` / `run.update_step(runId, stepId, status, notes)`\n- `run.attach_artifact(runId, stepId, artifact)`：保存日志/patch/测试结果\n- `run.rollback_hint(runId)`：给出回滚建议（你说可用外部 git，但工具仍可提示）\n\n### 资源与预算调度\n- `budget.get()` / `budget.consume(tokens,cost)`：控制上下文与 API 成本（可选）\n- `context.snapshot(save|load)`：保存/加载上下文快照（便于中断续跑）\n\n---\n\n## 8. Coder（实际改代码）\n目标：定位、修改、编译/运行、迭代修复。工具要覆盖“读-找-改-跑-诊断”。\n\n### 定位与理解\n- `code.search_symbols(...)`（共享）\n- `code.references.find(...)`（共享）\n- `fs.read_text(...)`（共享）\n- `code.symbol.get(symbolId)`：拿到签名/位置，辅助精确读取\n\n### 修改与重构（建议尽量结构化）\n- `fs.apply_patch(...)`（共享核心）\n- `code.refactor.rename(symbolId,newName)`（Roslyn 重构，可选但非常强）\n- `code.refactor.extract_method(range, name)` / `move_type(...)`（可选）\n- `code.format(path|project)`：格式化\n\n### 构建与测试\n- `build.run(target, configuration)`：封装 `dotnet build` 等\n- `test.run(scope, filter, configuration)`：封装 `dotnet test`\n- `lint.run(scope)`：StyleCop/Analyzers（如有）\n\n### 诊断与修复闭环\n- `diagnostics.get_latest()`：收集 IDE/编译诊断（错误码、位置、消息）\n- `diagnostics.explain(errorId)`：把常见错误映射到建议（可选）\n- `code.fix.apply(codeFixId, location)`：Roslyn CodeFix（可选）\n\n---\n\n## 9. Reviewer（审查：正确性/风格/风险/覆盖）\n目标：不只是“看 diff”，而是“验证是否满足计划验收”。\n\n### Diff 与审查辅助\n- `changes.list()` / `diff.preview(path)`（共享）\n- `diff.summary()`：汇总变更点、文件影响面\n- `code.quality.metrics(scope)`：复杂度、重复、依赖（可选）\n- `security.scan(scope)`：依赖漏洞/敏感信息（可选）\n\n### 验收验证\n- `test.run(...)`（共享）\n- `build.run(...)`（共享）\n- `acceptance.checklist.evaluate(plan, evidence)`：将测试日志/运行结果映射到验收条目（可选但很好用）\n\n---\n\n## 10. Publisher（发布/交付）\n目标：生成 release 产物、变更说明、版本号、打包、可选提交 PR。\n\n### 版本与发布\n- `version.bump(strategy)`：SemVer 自动递增（可选）\n- `changelog.generate(from,to,changes)`：生成变更说明\n- `package.build()`：产物打包（msix/zip/nuget 等视你的项目）\n- `publish.run(target)`：发布到指定渠道（可选）\n\n### Git/PR（你说可用外部工具，但这里列为可选）\n- `git.status()` / `git.commit(message)` / `git.branch(name)` / `git.tag(name)`\n- `git.diff()` / `git.log(range)`\n- `pr.create(title, body, reviewers)`（若集成平台）\n\n---\n\n## 11. RAG/知识库（跨 Agent 共享，但主用于 Inspector/TaskGenerator/Coder）\n你的 PDF RAG 已有，但代码场景也需要“代码 RAG”。\n\n- `rag.index.add_documents(source)`：把代码/文档入库（按 chunk 策略）\n- `rag.search(query, topK, filters)`：语义检索（返回 chunk + 引用位置）\n- `rag.get_chunk(chunkId)`：取具体片段\n- `rag.index.status()`：索引新鲜度/覆盖范围\n\n---\n\n## 关键建议（工具设计的“硬约束”）\n1. **所有工具输出结构化**：路径/范围/诊断都要可机器处理，避免纯文本难解析。\n2. **读代码要支持“范围读取+截断”**：否则 tokens 会失控，尤其你提到万行 cs。\n3. **索引优先，全文读取其次**：`search_symbols/references` 远比把 namespace/type/method 全塞 prompt 可靠。\n4. **构建/测试/诊断闭环必须工具化**：否则 Coder 只能“盲改”。\n5. **StepDispatcher 必须有 Run/Step 状态存储工具**：否则无法真正 workflow 化（中断续跑、审计、复现）。\n\n---\n\n如果你确认这套分层 OK，我下一步可以按你的实际项目现状（你已有：对话/toolcall、文件列表、Roslyn 分析、RAG、持久化）帮你做一次“工具缺口盘点”：\n- 现有工具 → 对应到上面清单\n- 还缺哪些最关键（按 ROI 排序）\n- 每个缺口给出最小可用的接口规范（参数/返回字段/错误码约定）";
        if (Parameters.Streaming)
        {
            var random = new Random();
            int currentIndex = 0;
            while (currentIndex < str.Length)
            {
                if (cancellationToken.IsCancellationRequested) break;

                int len = random.Next(3, 6); // 3 to 5 characters
                if (currentIndex + len > str.Length)
                {
                    len = str.Length - currentIndex;
                }

                var chunk = str.Substring(currentIndex, len);
                interactor?.Info(chunk);
                currentIndex += len;

                await Task.Delay(100, cancellationToken);
            }

            return new CompletedResult()
            {
                ResponseMessages = [new ChatMessage(ChatRole.Assistant, str)],
                Usage = new UsageDetails(),
                FinishReason = ChatFinishReason.Stop
            };
        }
        else
        {
            return new CompletedResult()
            {
                ResponseMessages = [new ChatMessage(ChatRole.Assistant, str)],
                Usage = new UsageDetails
                {
                    InputTokenCount = 0,
                    OutputTokenCount = 0,
                    TotalTokenCount = 0,
                    AdditionalCounts = null
                },
                Latency = 0,
                Duration = 0,
                ErrorMessage = null,
                Price = null,
                FinishReason = ChatFinishReason.Stop,
                Annotations = null,
                AdditionalProperties = null
            };
        }
    }

    public ILLMAPIEndpoint Endpoint { get; }
}

public class EmptyLlmModelClient : ILLMChatClient
{
    public static EmptyLlmModelClient Instance => new EmptyLlmModelClient();

    public string Name { get; } = "NullLlmModelClient";

    public ILLMAPIEndpoint Endpoint
    {
        get
        {
            return new APIEndPoint(new APIEndPointOption() { Name = "NullLlmModelClient" }, NullLoggerFactory.Instance);
        }
    }

    public IEndpointModel Model
    {
        get
        {
            return new APIModelInfo
            {
                APIId = "fake-model",
                Name = "Fake Model",
                IsNotMatchFromSource = false,
                Streaming = true,
                UrlIconEnable = false,
                IconType = ModelIconType.None,
                IconUrl = null,
                InfoUrl = "https://example.com/fake-model",
                Description = "This is a fake model for testing purposes.",
                MaxContextSize = 0,
                Endpoint = new EmptyLLMEndpoint(),
                SupportSystemPrompt = true,
                TopPEnable = true,
                TopKEnable = true,
                TemperatureEnable = true,
                MaxTokensEnable = true,
                FrequencyPenaltyEnable = true,
                PresencePenaltyEnable = true,
                SeedEnable = true,
                SystemPrompt = "默认系统提示",
                TopP = 0.9f,
                TopKMax = 100,
                TopK = 40,
                Temperature = 0.7f,
                MaxTokens = 2048,
                MaxTokenLimit = 4096,
                Reasonable = true,
                FunctionCallOnStreaming = true,
                SupportStreaming = true,
                SupportImageGeneration = true,
                SupportAudioGeneration = true,
                SupportVideoGeneration = true,
                SupportSearch = true,
                SupportFunctionCall = true,
                SupportAudioInput = true,
                SupportVideoInput = true,
                SupportTextGeneration = true,
                SupportImageInput = true,
                PriceCalculator = new TokenBasedPriceCalculator(),
                FrequencyPenalty = 0.5f,
                PresencePenalty = 0.5f,
                Seed = 42
            };
        }
    }

    public bool IsResponding { get; set; } = false;

    public IModelParams Parameters { get; set; } = new DefaultModelParam
    {
        Streaming = false,
        SystemPrompt = null,
        TopP = 0.9f,
        TopK = 40,
        Temperature = 0.7f,
        MaxTokens = 4096,
        FrequencyPenalty = 0.5f,
        PresencePenalty = 0.5f,
        Seed = 666
    };

    private readonly string? _fakeFilePath;

    public EmptyLlmModelClient(string? fakeFilePath = null)
    {
        this._fakeFilePath = fakeFilePath;
    }

    public async Task<CompletedResult> SendRequest(DialogContext context,
        IInvokeInteractor? stream = null,
        CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrEmpty(_fakeFilePath))
        {
            if (File.Exists(_fakeFilePath))
            {
                var fakeResponse = await File.ReadAllTextAsync(_fakeFilePath, cancellationToken);
                int next = Random.Shared.Next(8);
                int index = 0;
                while (index < fakeResponse.Length)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }

                    var chunk = fakeResponse.Substring(index, Math.Min(next, fakeResponse.Length - index));
                    stream?.Info(chunk);
                    index += next;
                    next = Random.Shared.Next(8);
                    await Task.Delay(200, cancellationToken);
                }
            }
        }

        return new CompletedResult
        {
            Usage = new UsageDetails
            {
                InputTokenCount = 0,
                OutputTokenCount = 0,
                TotalTokenCount = 0,
                AdditionalCounts = null
            },
            Latency = 0,
            Duration = 0,
            ErrorMessage = null,
            Price = null,
            FinishReason = ChatFinishReason.Stop,
            ResponseMessages =
            [
                new ChatMessage(ChatRole.Assistant, "This is a fake response from NullLlmModelClient.")
            ],
            Annotations = null,
            AdditionalProperties = null
        };
    }
}