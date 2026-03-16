---
applyTo: '**'
description: 'description'
---

# Project Overview

我在Windows上基于Semantic Kernel和Microsoft.Extension.AI开发了一个LLM桌面端软件，它实现三大功能：

1. 对话，基于OpenAI API实现了流式对话和ToolCall，基于Markdig和Markdig.WP将LLM的Markdown回复以FlowDocument渲染，最终将整个对话持久化管理。
2. LLM API的配置和管理，允许自定义填写LLM API的够供应商和具体模型参数。
3. RAG，基于Pdfpig进行PDF解析，执行Embedding后保留层次结构存储到Vector Store，最终执行向量检索。

## Folder Structure

- `/Abstraction`: "Contains abstract classes and interfaces for the project."
- `/Agent`: "Developing now, target to implement Agent framework based on Semantic Kernel."
- `/Component`: "Contains reusable UI components for the application."
- `/Configuration`: "Application configuration related settings."
- `/Data`: "Persistent data models and cache management."
- `/Dialog`: "Core module, responsible for managing conversations and interactions with LLM."
- `/Endpoints`: "LLM API endpoints, models and chat warpers for Semantic Kernel."
- `/Log`: "Simple logging utility for the application."
- `/Rag`: "Responsible for RAG related functionalities, including PDF parsing, embedding and vector store management."
- '/ToolCall': "Wraps ToolCall which contains MCP, Sementic Kernel Plugin, implement some common tools like web search,
  file management and so on."
- `/Workflow': "Developing now, target to implement Workflow framework based on Semantic Kernel, which is more flexible
  and powerful than Agent framework but requires more efforts to use."

## Libraries and Frameworks

- MaterialDesignThemes for UI components and theming.
- Markdig and Markdig.wpf for rendering Markdown responses from LLM.
- PdfPig for PDF parsing in RAG module.
- Semantic Kernel and Microsoft.Extensions.AI for LLM interactions and Agent/Workflow framework.
- AutoMapper for object mapping between different layers of the application.
- LatexToMathML for rendering mathematical formulas in Markdown responses.
- Roslyn for c# code analysis and generation.
