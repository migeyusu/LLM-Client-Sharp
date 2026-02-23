using System.Text.Json.Serialization;

namespace LLMClient.ContextEngineering.Analysis;

/// <summary>
/// 核心符号标识符 - LLM 调用工具时的核心参数
/// 设计思路：结合 SymbolKey 的可靠性与可读性
/// </summary>
public class SymbolIdentifier
{
    /// <summary>
    /// Roslyn SymbolKey 的 Base64 字符串，用于精确解析回 ISymbol
    /// </summary>
    [JsonPropertyName("key")]
    public string? SymbolKey { get; set; }

    /// <summary>
    /// 人类可读的完全限定名 (如 System.IO.File.Open)
    /// </summary>
    [JsonPropertyName("name")]
    public string? DisplayName { get; set; }

    /// <summary>
    /// 符号种类 (Class, Method, Field etc.)
    /// </summary>
    [JsonPropertyName("kind")]
    public string? Kind { get; set; }

    /// <summary>
    /// 所在文件路径 (相对项目根路径)
    /// </summary>
    [JsonPropertyName("source")]
    public string[]? Source { get; set; }

    public static SymbolIdentifier FromSymbol(MemberInfo symbol, string? basePath = null)
    {
        var id = symbol.UniqueId ?? symbol.Signature;
        return new SymbolIdentifier
        {
            SymbolKey = id,
            DisplayName = symbol.Signature,
            Kind = symbol.Kind,
            Source = symbol.FilesPath.ToArray()
        };
    }
}

/// <summary>
/// 轻量级符号摘要 - 用于列表展示，控制 Token 消耗
/// </summary>
public class SymbolSummary
{
    [JsonPropertyName("id")] public SymbolIdentifier Id { get; set; } = new();

    [JsonPropertyName("signature")] public string? Signature { get; set; }

    [JsonPropertyName("line")] public int LineNumber { get; set; }

    [JsonPropertyName("summary")] public string? XmlDocSummary { get; set; } // 仅第一行摘要

    // 可选：重要性评分，供 Planner 参考
    [JsonPropertyName("score")] public float RelevanceScore { get; set; }
}

/// <summary>
/// 文件系统项 - 用于 Map 视图
/// </summary>
public class FileSystemEntry
{
    [JsonPropertyName("path")] public string Path { get; set; } = string.Empty;

    [JsonPropertyName("type")] public string Type { get; set; } = "File"; // File, Folder, Project

    [JsonPropertyName("size")] public long SizeBytes { get; set; }

    [JsonPropertyName("loc")] public int LinesOfCode { get; set; }

    [JsonPropertyName("symbols")] public int SymbolCount { get; set; }
}

/*
 * ## 常用 SymbolDisplayFormat 预设与用途

| 预设 | 用途 | 特点 |
|------|------|------|
| `CSharpErrorMessageFormat` | C# 编译器错误消息中的符号显示 | 包含完整命名空间、参数类型、显式接口；不含可访问性；可读性强 [1](#12-0)  |
| `CSharpShortErrorMessageFormat` | C# 短错误消息 | 与上类似，但类型限定仅到包含类型（不含命名空间） [2](#12-1)  |
| `FullyQualifiedFormat` | 完全限定名（含 global::） | 包含全局命名空间前缀，适合跨项目唯一标识；不含成员参数 [3](#12-2)  |
| `MinimallyQualifiedFormat` | 最小限定（IDE 快速信息常用） | 省略全局命名空间，包含参数名与类型、包含类型；适合当前上下文显示 [4](#12-3)  |
| `VisualBasicErrorMessageFormat` | VB 错误消息 | 包含可访问性、修饰符、参数默认值等，更详细 [5](#12-4)  |

### 完整方法签名推荐
- **C# 场景**：`CSharpErrorMessageFormat`（含参数类型、显式接口、完整命名空间） [1](#12-0) 。
- **跨语言/文档**：`FullyQualifiedFormat`（仅类型/命名空间，不含参数；若需参数可自行扩展） [3](#12-2) 。
- **IDE 上下文**：`MinimallyQualifiedFormat`（常用，包含参数名与类型） [4](#12-3) 。

### IDE 层实际使用的签名格式
- CSharpSymbolDisplayService 中用于成员签名的 `s_memberSignatureDisplayFormat` 包含泛型约束、包含类型、参数名/类型、ref/扩展this、默认值等，适合完整签名展示 [6](#12-5) 。
- VB 对应的 `s_minimallyQualifiedFormat` 系列在 `VisualBasicSymbolDisplayService.SymbolDescriptionBuilder` 中定义，可参考其增删选项的方式 [7](#12-6) 。

### 使用方式
- 无上下文：`symbol.ToDisplayString(format)`
- 基于位置的最小化显示：`symbol.ToMinimalDisplayString(semanticModel, position, format)`（默认 MinimallyQualifiedFormat） [8](#12-7) 。

## Notes
- 若需包含可访问性、修饰符，可在 `CSharpErrorMessageFormat` 基础上通过 `WithMemberOptions` 添加 `IncludeAccessibility`/`IncludeModifiers`。
- 测试用例展示了 `FullyQualifiedFormat` 与 `MinimallyQualifiedFormat` 对同一符号的输出差异 [9](#12-8) 。
- `SymbolDisplayFormat` 支持通过 `Add/Remove/With` 方法微调选项，满足自定义需求 [10](#12-9) 。

Wiki pages you might want to explore:
- [Overview (dotnet/roslyn)](/wiki/dotnet/roslyn#1)

### Citations

**File:** src/Compilers/Core/Portable/SymbolDisplay/SymbolDisplayFormat.cs (L17-38)
```csharp
        public static SymbolDisplayFormat CSharpErrorMessageFormat { get; } =
            new SymbolDisplayFormat(
                globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.OmittedAsContaining,
                typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
                propertyStyle: SymbolDisplayPropertyStyle.NameOnly,
                genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
                memberOptions:
                    SymbolDisplayMemberOptions.IncludeParameters |
                    SymbolDisplayMemberOptions.IncludeContainingType |
                    SymbolDisplayMemberOptions.IncludeExplicitInterface,
                parameterOptions:
                    SymbolDisplayParameterOptions.IncludeParamsRefOut |
                    SymbolDisplayParameterOptions.IncludeType,
                miscellaneousOptions:
                    SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers |
                    SymbolDisplayMiscellaneousOptions.UseSpecialTypes |
                    SymbolDisplayMiscellaneousOptions.UseAsterisksInMultiDimensionalArrays |
                    SymbolDisplayMiscellaneousOptions.UseErrorTypeSymbolName |
                    SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

        internal static SymbolDisplayFormat CSharpErrorMessageNoParameterNamesFormat { get; } = CSharpErrorMessageFormat
            .AddCompilerInternalOptions(SymbolDisplayCompilerInternalOptions.ExcludeParameterNameIfStandalone);
```

**File:** src/Compilers/Core/Portable/SymbolDisplay/SymbolDisplayFormat.cs (L43-61)
```csharp
        public static SymbolDisplayFormat CSharpShortErrorMessageFormat { get; } =
            new SymbolDisplayFormat(
                globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.OmittedAsContaining,
                typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypes,
                propertyStyle: SymbolDisplayPropertyStyle.NameOnly,
                genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
                memberOptions:
                    SymbolDisplayMemberOptions.IncludeParameters |
                    SymbolDisplayMemberOptions.IncludeContainingType |
                    SymbolDisplayMemberOptions.IncludeExplicitInterface,
                parameterOptions:
                    SymbolDisplayParameterOptions.IncludeParamsRefOut |
                    SymbolDisplayParameterOptions.IncludeType,
                miscellaneousOptions:
                    SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers |
                    SymbolDisplayMiscellaneousOptions.UseSpecialTypes |
                    SymbolDisplayMiscellaneousOptions.UseAsterisksInMultiDimensionalArrays |
                    SymbolDisplayMiscellaneousOptions.UseErrorTypeSymbolName |
                    SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);
```

**File:** src/Compilers/Core/Portable/SymbolDisplay/SymbolDisplayFormat.cs (L66-94)
```csharp
        public static SymbolDisplayFormat VisualBasicErrorMessageFormat { get; } =
            new SymbolDisplayFormat(
                globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.OmittedAsContaining,
                typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
                propertyStyle: SymbolDisplayPropertyStyle.ShowReadWriteDescriptor,
                genericsOptions:
                    SymbolDisplayGenericsOptions.IncludeTypeParameters |
                    SymbolDisplayGenericsOptions.IncludeTypeConstraints |
                    SymbolDisplayGenericsOptions.IncludeVariance,
                memberOptions:
                    SymbolDisplayMemberOptions.IncludeParameters |
                    SymbolDisplayMemberOptions.IncludeAccessibility |
                    SymbolDisplayMemberOptions.IncludeType |
                    SymbolDisplayMemberOptions.IncludeRef |
                    SymbolDisplayMemberOptions.IncludeModifiers,
                kindOptions:
                    SymbolDisplayKindOptions.IncludeMemberKeyword,
                parameterOptions:
                    SymbolDisplayParameterOptions.IncludeParamsRefOut |
                    SymbolDisplayParameterOptions.IncludeExtensionThis |
                    SymbolDisplayParameterOptions.IncludeType |
                    SymbolDisplayParameterOptions.IncludeName |
                    SymbolDisplayParameterOptions.IncludeOptionalBrackets |
                    SymbolDisplayParameterOptions.IncludeDefaultValue,
                miscellaneousOptions:
                    SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers |
                    SymbolDisplayMiscellaneousOptions.UseSpecialTypes |
                    SymbolDisplayMiscellaneousOptions.UseAsterisksInMultiDimensionalArrays |
                    SymbolDisplayMiscellaneousOptions.UseErrorTypeSymbolName);
```

**File:** src/Compilers/Core/Portable/SymbolDisplay/SymbolDisplayFormat.cs (L137-144)
```csharp
        public static SymbolDisplayFormat FullyQualifiedFormat { get; } =
            new SymbolDisplayFormat(
                globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Included,
                typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
                genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
                miscellaneousOptions:
                    SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers |
                    SymbolDisplayMiscellaneousOptions.UseSpecialTypes);
```

**File:** src/Compilers/Core/Portable/SymbolDisplay/SymbolDisplayFormat.cs (L149-169)
```csharp
        public static SymbolDisplayFormat MinimallyQualifiedFormat { get; } =
            new SymbolDisplayFormat(
                globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
                genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
                memberOptions:
                    SymbolDisplayMemberOptions.IncludeParameters |
                    SymbolDisplayMemberOptions.IncludeType |
                    SymbolDisplayMemberOptions.IncludeRef |
                    SymbolDisplayMemberOptions.IncludeContainingType,
                kindOptions:
                    SymbolDisplayKindOptions.IncludeMemberKeyword,
                parameterOptions:
                    SymbolDisplayParameterOptions.IncludeName |
                    SymbolDisplayParameterOptions.IncludeType |
                    SymbolDisplayParameterOptions.IncludeParamsRefOut |
                    SymbolDisplayParameterOptions.IncludeDefaultValue,
                localOptions: SymbolDisplayLocalOptions.IncludeType,
                miscellaneousOptions:
                    SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers |
                    SymbolDisplayMiscellaneousOptions.UseSpecialTypes |
                    SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);
```

**File:** src/Features/Core/Portable/LanguageServices/SymbolDisplayService/AbstractSymbolDisplayService.AbstractSymbolDescriptionBuilder.cs (L40-68)
```csharp
        private static readonly SymbolDisplayFormat s_memberSignatureDisplayFormat =
            new(globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
                genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters | SymbolDisplayGenericsOptions.IncludeTypeConstraints,
                memberOptions:
                    SymbolDisplayMemberOptions.IncludeRef |
                    SymbolDisplayMemberOptions.IncludeType |
                    SymbolDisplayMemberOptions.IncludeParameters |
                    SymbolDisplayMemberOptions.IncludeContainingType,
                kindOptions:
                    SymbolDisplayKindOptions.IncludeMemberKeyword,
                propertyStyle:
                    SymbolDisplayPropertyStyle.ShowReadWriteDescriptor,
                parameterOptions:
                    SymbolDisplayParameterOptions.IncludeName |
                    SymbolDisplayParameterOptions.IncludeType |
                    SymbolDisplayParameterOptions.IncludeParamsRefOut |
                    SymbolDisplayParameterOptions.IncludeExtensionThis |
                    SymbolDisplayParameterOptions.IncludeDefaultValue |
                    SymbolDisplayParameterOptions.IncludeOptionalBrackets,
                localOptions:
                    SymbolDisplayLocalOptions.IncludeRef |
                    SymbolDisplayLocalOptions.IncludeType,
                miscellaneousOptions:
                    SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers |
                    SymbolDisplayMiscellaneousOptions.UseSpecialTypes |
                    SymbolDisplayMiscellaneousOptions.UseErrorTypeSymbolName |
                    SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier |
                    SymbolDisplayMiscellaneousOptions.AllowDefaultLiteral |
                    SymbolDisplayMiscellaneousOptions.CollapseTupleTypes);
```

**File:** src/Features/VisualBasic/Portable/LanguageServices/VisualBasicSymbolDisplayService.SymbolDescriptionBuilder.vb (L17-27)
```text
            Private Shared ReadOnly s_minimallyQualifiedFormat As SymbolDisplayFormat = SymbolDisplayFormat.MinimallyQualifiedFormat _
                .AddMiscellaneousOptions(SymbolDisplayMiscellaneousOptions.UseErrorTypeSymbolName) _
                .RemoveParameterOptions(SymbolDisplayParameterOptions.IncludeDefaultValue)

            Private Shared ReadOnly s_minimallyQualifiedFormatWithConstants As SymbolDisplayFormat = s_minimallyQualifiedFormat _
                .AddLocalOptions(SymbolDisplayLocalOptions.IncludeConstantValue) _
                .AddMemberOptions(SymbolDisplayMemberOptions.IncludeConstantValue) _
                .AddParameterOptions(SymbolDisplayParameterOptions.IncludeDefaultValue)

            Private Shared ReadOnly s_minimallyQualifiedFormatWithConstantsAndModifiers As SymbolDisplayFormat = s_minimallyQualifiedFormatWithConstants _
                .AddMemberOptions(SymbolDisplayMemberOptions.IncludeModifiers)
```

**File:** src/Compilers/Core/Portable/Symbols/ISymbol.cs (L266-269)
```csharp
        string ToMinimalDisplayString(
            SemanticModel semanticModel,
            int position,
            SymbolDisplayFormat? format = null);
```

**File:** src/Compilers/CSharp/Test/Symbol/SymbolDisplay/SymbolDisplayTests.cs (L118-146)
```csharp
        [Fact]
        public void TestFullyQualifiedFormat()
        {
            var text = @"
namespace N1 {
    namespace N2.N3 {
        class C1 {
            class C2 {} } } }
";

            Func<NamespaceSymbol, Symbol> findSymbol = global =>
                global.GetNestedNamespace("N1").
                GetNestedNamespace("N2").
                GetNestedNamespace("N3").
                GetTypeMembers("C1").Single().
                GetTypeMembers("C2").Single();

            TestSymbolDescription(
                text,
                findSymbol,
                SymbolDisplayFormat.FullyQualifiedFormat,
                "global::N1.N2.N3.C1.C2",
                SymbolDisplayPartKind.Keyword, SymbolDisplayPartKind.Punctuation,
                SymbolDisplayPartKind.NamespaceName, SymbolDisplayPartKind.Punctuation,
                SymbolDisplayPartKind.NamespaceName, SymbolDisplayPartKind.Punctuation,
                SymbolDisplayPartKind.NamespaceName, SymbolDisplayPartKind.Punctuation,
                SymbolDisplayPartKind.ClassName, SymbolDisplayPartKind.Punctuation,
                SymbolDisplayPartKind.ClassName);
        }
```

**File:** src/Tools/SemanticSearch/ReferenceAssemblies/Apis/Microsoft.CodeAnalysis.txt (L3007-3025)
```text
Microsoft.CodeAnalysis.SymbolDisplayFormat.AddGenericsOptions(Microsoft.CodeAnalysis.SymbolDisplayGenericsOptions)
Microsoft.CodeAnalysis.SymbolDisplayFormat.AddKindOptions(Microsoft.CodeAnalysis.SymbolDisplayKindOptions)
Microsoft.CodeAnalysis.SymbolDisplayFormat.AddLocalOptions(Microsoft.CodeAnalysis.SymbolDisplayLocalOptions)
Microsoft.CodeAnalysis.SymbolDisplayFormat.AddMemberOptions(Microsoft.CodeAnalysis.SymbolDisplayMemberOptions)
Microsoft.CodeAnalysis.SymbolDisplayFormat.AddMiscellaneousOptions(Microsoft.CodeAnalysis.SymbolDisplayMiscellaneousOptions)
Microsoft.CodeAnalysis.SymbolDisplayFormat.AddParameterOptions(Microsoft.CodeAnalysis.SymbolDisplayParameterOptions)
Microsoft.CodeAnalysis.SymbolDisplayFormat.RemoveGenericsOptions(Microsoft.CodeAnalysis.SymbolDisplayGenericsOptions)
Microsoft.CodeAnalysis.SymbolDisplayFormat.RemoveKindOptions(Microsoft.CodeAnalysis.SymbolDisplayKindOptions)
Microsoft.CodeAnalysis.SymbolDisplayFormat.RemoveLocalOptions(Microsoft.CodeAnalysis.SymbolDisplayLocalOptions)
Microsoft.CodeAnalysis.SymbolDisplayFormat.RemoveMemberOptions(Microsoft.CodeAnalysis.SymbolDisplayMemberOptions)
Microsoft.CodeAnalysis.SymbolDisplayFormat.RemoveMiscellaneousOptions(Microsoft.CodeAnalysis.SymbolDisplayMiscellaneousOptions)
Microsoft.CodeAnalysis.SymbolDisplayFormat.RemoveParameterOptions(Microsoft.CodeAnalysis.SymbolDisplayParameterOptions)
Microsoft.CodeAnalysis.SymbolDisplayFormat.WithGenericsOptions(Microsoft.CodeAnalysis.SymbolDisplayGenericsOptions)
Microsoft.CodeAnalysis.SymbolDisplayFormat.WithGlobalNamespaceStyle(Microsoft.CodeAnalysis.SymbolDisplayGlobalNamespaceStyle)
Microsoft.CodeAnalysis.SymbolDisplayFormat.WithKindOptions(Microsoft.CodeAnalysis.SymbolDisplayKindOptions)
Microsoft.CodeAnalysis.SymbolDisplayFormat.WithLocalOptions(Microsoft.CodeAnalysis.SymbolDisplayLocalOptions)
Microsoft.CodeAnalysis.SymbolDisplayFormat.WithMemberOptions(Microsoft.CodeAnalysis.SymbolDisplayMemberOptions)
Microsoft.CodeAnalysis.SymbolDisplayFormat.WithMiscellaneousOptions(Microsoft.CodeAnalysis.SymbolDisplayMiscellaneousOptions)
Microsoft.CodeAnalysis.SymbolDisplayFormat.WithParameterOptions(Microsoft.CodeAnalysis.SymbolDisplayParameterOptions)
```

 */