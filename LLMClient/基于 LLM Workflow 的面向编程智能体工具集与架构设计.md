# 软件设计规范 (SDD)：基于 LLM Workflow 的面向编程智能体工具集与架构设计

## 1. 项目背景与基础架构 (Context & Background)

本项目是一个在 Windows 平台上运行的桌面端大语言模型 (LLM) 应用程序，其底层基于 **Semantic Kernel** 和 **Microsoft.Extensions.AI** 构建。系统的核心基础设施已具备以下三大基础能力：
1. **富文本对话与交互**：基于 OpenAI API 标准实现了流式对话输出与 ToolCall（工具调用）支持。前端基于 Markdig 和 Markdig.WP 库，将 LLM 响应的 Markdown 文本深度渲染为 WPF 的 FlowDocument，并实现了整个对话链路的持久化管理。
2. **多模型接入与配置**：提供灵活的 LLM API 供应商和模型参数配置界面，支持解耦模型能力与具体业务逻辑。
3. **检索增强生成 (RAG) 系统**：基于 PdfPig 解析 PDF 及其他文档类型的物理和逻辑层次，执行文本 Embedding 嵌入后，将带有层次结构的元数据持久化至本地 Vector Store，支持高精度的向量检索与混合检索。

**最终演进目标**：在上述基础设施之上，构建一个**面向编程的 LLM Workflow**，使其能够理解本地代码库、生成架构方案并按计划自动执行代码开发任务。

## 2. 工作流设计要求架构 (Workflow Architecture)

系统的核心理念是通过显式的 Workflow 与多 Agent 协同来控制系统的行为边界。整个生命周期被严格划分为**架构设计阶段（Scheme Phase）**与**执行阶段（Execution Phase）**。

### 2.1 核心约束
* **有限的并行原则**：为了控制资源开销与系统复杂度，并行推理（优中选优）**特许且仅允许**在架构阶段（Scheme Phase）出现。代码执行阶段严格按照既定 Task 列表串行或按序执行。
* **人在回路与可回滚性**：从 Plan 制定到最终 Release 的完整流程，在技术上必须具备重置/重播能力，通过外部 Git 等版本控制工具保障代码修改的可回滚性。在关键节点上，用户可以对 Plan 予以批准或拒绝。

### 2.2 工作流链路
系统根据任务复杂度，由策略路由（SchemeRouter）控制走简单或复杂工作流：

* **简单流程**：User Prompt -> Inspector（全景上下文分析）-> TaskGenerator（需求与任务拆解）-> SchemeRouter（路由至简单分支）-> Architect（单一推演，生成架构图）-> Planner（制定明确 Step 列表）-> StepDispatcher（调度引擎）-> Coder（代码编写与迭代）-> Reviewer（审查）-> StepDispatcher（校验是否执行完毕）-> Publisher（合并发布）。
* **复杂流程**：User Prompt -> Inspector -> TaskGenerator -> SchemeRouter（路由至复杂分支）-> **多个 Architect 结合 Reasoning 模型并行推演架构** -> Refiner（精炼、合并与决策优胜架构）-> Planner -> StepDispatcher -> Coder -> Reviewer -> StepDispatcher -> Publisher。

---

## 3. 规范定义：Agent 工具集按职责划分 (Toolset Specification)

本部分的工具集规划以基于“职责域”维度组织（而不是与 Agent 强绑定），使得具备高内聚低耦合的特性，方便 Semantic Kernel 的 Plugin 系统进行跨 Agent 的复用与注册。

> **以下为核心工具集规划：**

### 一、项目感知（Project Awareness）

> **主要消费方：Inspector、TaskGenerator**  
> 这是整个 Workflow 的起点，Inspector 依赖这组工具完成对工作区的初始理解。

| 工具 | 说明 |
|------|------|
| `get_solution_info` | 解决方案下所有项目、目标框架、语言版本、输出类型 |
| `get_project_metadata(projectId)` | 包引用、项目引用、编译配置、条件编译符号 |
| `get_file_tree(path, depth, excludePatterns)` | 目录树，支持深度控制与噪音目录排除 |
| `get_file_metadata(path)` | 行数、大小、最后修改时间、token 估算、文件类型标签 |
| `detect_conventions` | 检测命名惯例与代码风格（基于 .editorconfig / StyleCop 配置） |
| `get_recently_modified_files(since?, count?)` | 最近被修改的文件列表，用于快速感知活跃区域 |

### 二、代码读取（Code Reading）

> **主要消费方：TaskGenerator、Architect、Planner、Coder、Reviewer**  
> 整个 Workflow 使用频率最高的基础工具组，几乎所有 Agent 都依赖。

| 工具 | 说明 |
|------|------|
| `read_file(path, startLine?, endLine?, maxTokens?)` | 读取文件全部或片段，超出 maxTokens 时截断并标记 `truncated: true` |
| `read_symbol_body(symbolId, contextLines?)` | 读取指定符号的实现体及其前后 N 行上下文 |
| `get_file_outline(path)` | 只返回文件内所有类型及成员的**签名列表**，不含实现体，高可读低 token |
| `list_files(path, filter?, recursive?)` | 文件列表，带路径/大小/标签，可按后缀或模式过滤 |

> **注意**：`get_file_outline` 专为解决超大文件问题设计——单文件上万行时，先通过 outline 看清结构，再按需 `read_symbol_body`。

### 三、符号与语义分析（Symbol & Semantic Analysis）

> **主要消费方：Inspector、TaskGenerator、Architect、Planner、Coder**  
> 基于 Roslyn 的深度分析能力，是项目的核心竞争力所在。

| 工具 | 说明 |
|------|------|
| `search_symbols(query, kind?, scope?)` | 按名称/特性/关键词搜索符号，返回 symbolId / signature / location / score |
| `get_symbol_detail(symbolId)` | 签名、XML 注释、文件位置、可见性、所属类型/命名空间 |
| `get_type_members(typeId, filter?)` | 类型的所有成员签名（可按可见性、类型过滤） |
| `get_type_hierarchy(typeId)` | 基类链、接口实现、已知派生类 |
| `get_interface_implementations(interfaceId)` | 接口的所有实现类（含项目外引用） |
| `get_callers(symbolId, scope?)` | 所有调用该符号的位置（调用图入边） |
| `get_callees(symbolId)` | 该符号调用的所有其他符号（调用图出边） |
| `get_usages(symbolId)` | 所有引用位置，区分读/写/初始化 |
| `get_dependency_graph(projectId, depth?)` | 项目或文件级别的模块依赖关系 |
| `get_namespace_types(namespace)` | 命名空间下的所有类型列表 |

### 四、代码搜索（Code Search）

> **主要消费方：TaskGenerator、Coder、Reviewer**  
> 当符号名称未知或需要跨文件定位时使用。

| 工具 | 说明 |
|------|------|
| `search_text(pattern, scope?, fileFilter?)` | 文本/正则搜索，返回文件路径 + 行号 + 行内容摘要 |
| `search_semantic(query, topK?)` | 基于 embedding 的语义检索，返回最相关代码片段（依托现有的 RAG 基础设施） |
| `find_similar_code(snippet, topK?)` | 传入代码片段，检索语义相似的实现，用于发现重复逻辑 |
| `find_by_attribute(attributeName, scope?)` | 查找所有标注了特定特性的类型或方法 |
| `search_in_file(path, pattern)` | 在单文件内的文本搜索，返回匹配行列表 |

### 五、代码修改（Code Mutation）

> **主要消费方：Coder（独占写权限）**  
> 这是整个工具集中**唯一具有写副作用**的组。其他所有 Agent 均为只读。

| 工具 | 说明 |
|------|------|
| `create_file(path, content)` | 创建新文件，路径不存在时自动创建目录 |
| `overwrite_file(path, content)` | 整体替换文件内容（用于大段重构） |
| `apply_patch(path, unifiedDiff)` | 应用 unified diff，失败时返回冲突位置，不静默覆盖 |
| `insert_lines(path, afterLine, content)` | 在指定行后插入内容 |
| `delete_lines(path, startLine, endLine)` | 删除行范围 |
| `rename_symbol(symbolId, newName)` | Roslyn 重命名，自动更新所有引用（安全重构） |
| `move_type(typeId, targetPath, targetNamespace)` | 将类型迁移到目标文件与命名空间 |
| `add_import(path, namespace)` | 插入 using 指令（自动去重） |
| `delete_file(path)` | 删除文件 |
| `move_file(src, dest)` | 移动或重命名文件 |
| `create_directory(path)` | 创建目录结构 |

### 六、构建与诊断（Build & Diagnostics）

> **主要消费方：Coder（迭代修复循环）、Reviewer、Publisher**  
> Coder 的核心工作循环之一：写代码 → 构建 → 读诊断 → 修复，直到无错误。

| 工具 | 说明 |
|------|------|
| `build_project(projectId?)` | 触发编译，返回成功/失败及错误摘要列表 |
| `get_diagnostics(path?)` | 获取 Roslyn/IDE 当前报告的错误与警告，可按文件筛选 |
| `get_build_errors()` | 最近一次构建的详细错误列表（路径、行号、错误码、消息、严重性） |
| `get_code_analysis_issues(path?)` | 静态分析警告（Roslyn Analyzer、StyleCop 等规则违反） |
| `run_code_format(path?)` | 对指定文件或全项目执行格式化（依据 .editorconfig） |

### 七、测试（Testing）

> **主要消费方：Reviewer、Publisher**  
> Reviewer 用于验证变更不破坏现有行为，Publisher 用于最终质量门控。

| 工具 | 说明 |
|------|------|
| `run_tests(filter?, projectId?)` | 执行测试，返回通过/失败/跳过数量摘要 |
| `get_test_results()` | 最近一次测试运行的详细结果，含失败用例与堆栈 |
| `find_tests_for_symbol(symbolId)` | 查找覆盖特定符号的测试用例（用于精准验证） |
| `get_test_coverage(path? or symbolId?)` | 行/分支覆盖率信息 |

### 八、版本控制（VCS）

> **主要消费方：Inspector（感知变更范围）、Publisher（生成变更描述）**  
> Inspector 通过 VCS 确定本次任务真正影响的代码范围，避免分析全量代码。

| 工具 | 说明 |
|------|------|
| `get_git_status()` | 当前工作区未提交变更（new / modified / deleted） |
| `get_file_diff(path)` | 工作区与 HEAD 的 diff |
| `get_recent_commits(path?, count?)` | 文件或项目的最近 commit 历史 |
| `get_changed_files_since(ref)` | 自某 commit 或分支以来的变更文件集合 |
| `get_blame(path, startLine, endLine)` | 行级别的变更归属，用于理解历史意图 |

### 九、任务与状态管理（Task & Plan State）

> **主要消费方：Planner（创建）、StepDispatcher（调度）、所有 Agent（只读查询）**  
> Workflow 的"神经中枢"，记录整个执行过程的状态，支持 StepDispatcher 的循环判断。

| 工具 | 说明 |
|------|------|
| `get_plan()` | 获取当前计划的完整步骤列表及每步状态 |
| `get_current_step()` | 获取当前正在执行的步骤详情（编号、描述、前置条件） |
| `update_step_status(stepId, status, notes?)` | 标记步骤为 `running / done / failed / skipped` |
| `report_blocker(stepId, reason)` | 标记步骤为 `blocked` 并附原因，用于触发人工干预 |
| `append_step_observation(stepId, content)` | Agent 向步骤追加执行中的观察或中间产出 |
| `get_completed_steps()` | 查询已完成步骤的摘要，防止重复工作 |

### 十、架构与文档（Architecture & Documentation）

> **主要消费方：Architect、Refiner、Publisher**  
> Architect 需要生成可视化方案，Publisher 需要整理变更说明。

| 工具 | 说明 |
|------|------|
| `generate_class_diagram(typeIds)` | 基于 Roslyn 数据生成 Mermaid / PlantUML 类图文本 |
| `generate_dependency_diagram(scope)` | 生成模块/项目依赖关系图（Mermaid 格式） |
| `get_existing_design_docs(path?)` | 读取已有设计文档、ADR、README |
| `get_nuget_package_api(packageName, typeName?)` | 查询 NuGet 包的公开 API 摘要，供 Architect 设计时参考 |
| `generate_change_summary(stepIds)` | 基于已完成步骤生成变更摘要（供 Publisher 用） |

---

## 4. 关键设计原则设计 (Core Principles)

```text
写权限边界：只有 Coder 可调用「代码修改」组工具。
其他所有 Agent 严格只读。
这使得整个 Workflow 中的副作用来源单一、可审计。
```

除独占的安全边线设计外，在 Semantic Kernel 集成落地层面，该契约设计将利用 C# 类型或 JSON Schema 提供强类型的工具签名，保证 LLM 解析 ToolCall 请求时具备高度一致性和高成功率。这些工具的具体行为由桌面端的宿主上下文实现（包括 Roslyn Workspace 桥接机制、本地 `System.IO`、本地 `git cli` 等），不在 Prompt 或系统角色中堆叠暴露具体代码。

---

## 5. 各 Agent 与工具组的依赖矩阵 (Dependency Matrix)

本表格约束了 Semantic Kernel 为任意特定环节注册工具时的注入范围。仅注册当前 Agent 所需的最小特权功能，避免 Agent 工具幻觉或误调。

| Agent | 项目感知 | 代码读取 | 符号分析 | 代码搜索 | 代码修改 | 构建诊断 | 测试 | VCS | 任务状态 | 架构文档 |
|---|:---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| Inspector | ✅ | ✅ | ✅ | | | | | ✅ | | |
| TaskGenerator | ✅ | ✅ | ✅ | ✅ | | | | | ✅ | |
| SchemeRouter | | | ✅ | | | | | | ✅ | |
| Architect | | ✅ | ✅ | ✅ | | | | | ✅ | ✅ |
| Refiner | | ✅ | ✅ | | | | | | ✅ | ✅ |
| Planner | | ✅ | ✅ | ✅ | | | | | ✅ | |
| StepDispatcher | | | | | | | | | ✅ | |
| Coder | | ✅ | ✅ | ✅ | ✅ | ✅ | | | ✅ | |
| Reviewer | | ✅ | ✅ | ✅ | | ✅ | ✅ | ✅ | ✅ | |
| Publisher | | | | | | ✅ | ✅ | ✅ | ✅ | ✅ |