[English](README.md) | [简体中文](README.zh-CN.md)

# LLM Client for WPF

一个基于 `.NET` 和 `WPF` 技术实现的开源大语言模型 (LLM) 客户端项目，旨在提供一种轻量级、直观且功能丰富的交互工具，用于使用多种支持的 LLM 服务。本项目默认支持 GitHub Copilot 服务下提供的一些模型（如 `GPT-4o`、`O1` 和 `DeepSeek`），并可通过扩展支持其他服务终结点。

## 主要特性

1. **纯 .NET WPF 实现**
   - 使用 `MaterialDesign` 实现现代化界面设计。
   - 利用 `Microsoft.Extensions.AI` 用于集成大语言模型 API。
   
2. **模型交互功能**
   - 支持基础的模型配置和对话功能。

3. **代码高亮**
   - 集成 `TextmateSharp`，支持多种语言的代码高亮显示。

4. **上下文管理**
   - 支持手动管理对话上下文，例如筛除任意一条对话上下文的记录，无需删除即可标记为不被传递。

5. **主题切换**
   - 提供明亮主题和暗黑主题，两套 UI 风格自由切换。
   - 支持代码高亮主题选择，适配不同用户习惯。

6. **UI 性能优化**
   - 对话记录实现了 UI 虚拟化，提升性能，满足大规模对话数据处理需求。

7. **Markdown 导出**
   - 支持将对话记录以 Markdown 格式导出，便于存档和分享。

## 待完成特性

以下功能正在计划开发中：

1. **多服务终结点支持**
   - 计划支持接入其他大语言模型服务，例如 `Claude`。

2. **编排预设 CoT**
   - 支持编排基于 Chain-of-Thought (CoT) 的推理过程，帮助用户更高效地获得多步推理输出。

3. **Auto-CoT**
   - 自动生成 Chain-of-Thought 推理，提升复杂任务的自动化处理效果。

4. **RAG 能力支持**
   - 引入 Retrieval-Augmented Generation (RAG)，实现基于检索的高级生成功能。

5. **自动上下文管理**
   - 提供上下文的智能管理功能，无需手动排除历史记录。

6. **多模型输出对比**
   - 支持不同模型间的输出对比，为模型选择提供更直观依据。

7. **搜索功能**
   - 提供对话记录与知识库内容的快速搜索能力。

## 如何参与项目

本项目尚在开发阶段，您可以通过以下方式参与：

1. 提交 Issue 或 PR：任何关于功能反馈、Bug 修复或者新特性的建议都非常欢迎！
2. 成为贡献者：直接 Fork 本项目，并发起 Pull Request。
3. 联系作者：如果有任何问题或者合作意向，可以通过 [GitHub Issues](https://github.com/) 联系我。

## 项目截图

> 待补充截图展示 UI 界面和使用效果。

## 使用方法

> 待详细添加项目的编译、运行和配置说明。

## 感谢

感谢以下开源库和工具的支持：

- [MaterialDesignInXAML](https://github.com/MaterialDesignInXAML/MaterialDesignInXamlToolkit)
- [TextmateSharp](https://github.com/microsoft/TextMateSharp)
- [Microsoft.Extensions.AI](https://learn.microsoft.com/en-us/dotnet/)

---

这是一个充满潜力的项目，欢迎大家加入进来，共同扩展其应用场景！
