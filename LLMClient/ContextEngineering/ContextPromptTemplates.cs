// ContextPromptTemplates.cs

namespace LLMClient.ContextEngineering;

/// <summary>
/// 上下文工程的 Prompt 模板集合
/// </summary>
public static class ContextPromptTemplates
{
    /// <summary>
    /// 主模板：将原始 System Prompt 与代码上下文融合
    /// </summary>
    public const string MainSystemPromptTemplate = """
                                                   {{$originalSystemPrompt}}

                                                   {{$codeContextSection}}
                                                   """;

    /// <summary>
    /// 代码上下文主段落模板
    /// 当存在任何代码上下文时使用
    /// </summary>
    public const string CodeContextSectionTemplate = """
                                                     <code_context>   
                                                     <description>
                                                     You have access to the following codebase context to assist with coding tasks.
                                                     Use this information to provide accurate, contextually relevant responses.
                                                     </description>
                                                     
                                                     <ProjectStructure>
                                                     {{$projectContext}}
                                                     </ProjectStructure>
                                                     
                                                     {{$focusedContext}}
                                                     
                                                     {{$relevantSnippets}}

                                                     <usage_guidelines>
                                                     1. **Map vs. Territory**: The <ProjectStructure> provides namespaces, classes, and method signatures, but NOT the implementation details (method bodies).
                                                     2. **Navigation Strategy**:
                                                        - When asked to implement a feature, FIRST check <ProjectStructure> to see if relevant types or helpers already exist.
                                                        - Do NOT guess implementation logic. If you need to see the code inside a method (e.g., `ProcessData`), use your file reading tool to read the file specified in the `FilePath`.
                                                     3. **Consistency**:
                                                        - Use the exact Namespaces and Type names defined in the structure.
                                                        - Follow the naming conventions observed in the existing method signatures.
                                                        - Respect the `TargetFramework` and defined `PackageReferences` (e.g., do not suggest Newtonsoft.Json if the project uses System.Text.Json).
                                                     4. **Architecture Awareness**:
                                                        - Identify the project boundaries (e.g., separate Core/Domain projects from UI/Infrastructure).
                                                        - Do not suggest circular dependencies between defined projects.
                                                     </usage_guidelines>
                                                     </code_context>
                                                     """;
    

    /// <summary>
    /// 当前聚焦上下文模板（用户正在编辑的文件/类型）
    /// </summary>
    public const string FocusedContextTemplate = """
                                                 <focused_context>
                                                 <current_file path="{{$filePath}}">
                                                 <containing_type>
                                                 {{$typeInfo}}
                                                 </containing_type>

                                                 <visible_members>
                                                 {{$memberSignatures}}
                                                 </visible_members>

                                                 <related_types>
                                                 {{$relatedTypes}}
                                                 </related_types>
                                                 </current_file>
                                                 </focused_context>
                                                 """;

    /// <summary>
    /// RAG 检索到的相关代码片段模板
    /// </summary>
    public const string RelevantSnippetsTemplate = """
                                                   <relevant_snippets query="{{$searchQuery}}">
                                                   {{$snippetsList}}
                                                   </relevant_snippets>
                                                   """;

    /// <summary>
    /// 单个代码片段模板
    /// </summary>
    public const string CodeSnippetTemplate = """
                                              <snippet source="{{$sourcePath}}" relevance="{{$relevanceScore}}">
                                              <signature>{{$signature}}</signature>
                                              <summary>{{$summary}}</summary>
                                              <code>
                                              {{$codeContent}}
                                              </code>
                                              </snippet>
                                              """;
}