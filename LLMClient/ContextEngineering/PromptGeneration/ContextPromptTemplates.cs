// ContextPromptTemplates.cs

namespace LLMClient.ContextEngineering.PromptGeneration;

/// <summary>
/// 上下文工程的 Prompt 模板集合
/// </summary>
public static class ContextPromptTemplates
{

    public const string ProjectStructureGuideLines = """
                                                     <ProjectStructureGuidelines>
                                                         <Description>
                                                             The provided1 context is a structured Markdown summary of a software solution or a single project. It is designed to help you quickly understand the project's architecture, dependencies, and locate code definitions. The summary may be for a whole solution (starting with '# Solution Summary') or a single project (starting with '# Project Summary').
                                                         </Description>
                                                         <StructureAndSyntax>
                                                             <Hierarchy>
                                                                 The content is organized hierarchically:
                                                                 1.  **Solution/Project**: Top-level information.
                                                                 2.  **Overview**: Key statistics (file counts, lines of code, etc.).
                                                                 3.  **Type Distribution**: (Solution only) A breakdown of code elements by kind (e.g., Class, Interface).
                                                                 4.  **Projects**: Each project is detailed under a `### Project Name` heading.
                                                                 5.  **Structure**: Inside each project, code is grouped by namespace (`- **NamespaceName**`).
                                                                 6.  **Types**: Types are listed under their namespace.
                                                                 7.  **Members**: Important members are listed under their type.
                                                             </Hierarchy>
                                                             <TypeLocationSyntax>
                                                                 **Crucial**: Type definitions include a direct reference to their source file location. This is the primary way to locate code.
                                                                 -   **Format**: `- `TypeName` (Kind)*@RelativeFilePath:LineNumber*`
                                                                 -   **Example**: `- `MyService` (Class)*@Services/MyService.cs:12*`
                                                                 -   **Interpretation**: The class `MyService` is defined in the file `Services/MyService.cs` at line 12. Use this path to request the full source code or navigate the project.
                                                             </TypeLocationSyntax>
                                                             <MemberSyntax>
                                                                 -   **Format**: `- MemberSignature (Kind)`
                                                                 -   **Example**: `- string GetName() (Method)`
                                                                 -   **Interpretation**: Represents a method or property within a type.
                                                             </MemberSyntax>
                                                         </StructureAndSyntax>
                                                         <ContentVisibility>
                                                             <FocusOnImportance>
                                                                 The summary is intentionally filtered to show only \"Important\" code elements to conserve space and highlight key components.
                                                                 -   **Important Types**: Publicly visible types, or types with attributes containing \"Controller\", \"Service\", or \"Repository\".
                                                                 -   **Important Members**: Public methods/properties, or members with routing attributes like \"[HttpGet]\", \"[HttpPost]\", or \"[Route]\".
                                                             </FocusOnImportance>
                                                             <Truncation>
                                                                 Be aware that lists may be truncated for brevity.
                                                                 -   If you see a line like `... and N more`, it means the full list (e.g., of packages, types, or members) is not shown.
                                                                 -   Private or internal helpers are generally omitted unless they meet the \"Importance\" criteria. If you need details on a specific implementation that is not visible, you may need to ask for the full content of the file indicated by the `TypeLocationSyntax`.
                                                             </Truncation>
                                                         </ContentVisibility>
                                                     </ProjectStructureGuidelines>
                                                     """;

    public const string ProjectStructureTemplate = """
                                                   <ProjectStructure>
                                                   {{{projectStructure}}}
                                                   </ProjectStructure>

                                                   {{{ProjectStructureGuideLines}}}
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

                                                     {{{projectContext}}}

                                                     {{{focusedContext}}}

                                                     {{{relevantSnippets}}}

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