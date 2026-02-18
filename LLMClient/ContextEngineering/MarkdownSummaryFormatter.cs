using System.Text;
using LLMClient.ContextEngineering.Analysis;

namespace LLMClient.ContextEngineering;

public class MarkdownSummaryFormatter
{
    private readonly FormatterOptions _options;

    public MarkdownSummaryFormatter(FormatterOptions? options = null)
    {
        _options = options ?? new FormatterOptions();
    }

    public string Format(SolutionInfo summary)
    {
        var sb = new StringBuilder();

        // 头部信息
        sb.AppendLine($"# Solution Summary: {summary.SolutionName}");
        sb.AppendLine();
        sb.AppendLine($"Generated: {summary.GeneratedAt:yyyy-MM-dd HH:mm:ss UTC}");
        sb.AppendLine($"Generation Time: {summary.GenerationTime.TotalSeconds:F2}s");
        sb.AppendLine();

        // 统计概览
        sb.AppendLine("## Overview");
        sb.AppendLine();
        sb.AppendLine($"- **Projects**: {summary.Statistics.TotalProjects}");
        sb.AppendLine($"- **Files**: {summary.Statistics.FilesCount}");
        sb.AppendLine($"- **Types**: {summary.Statistics.TypesCount}");
        sb.AppendLine($"- **Methods**: {summary.Statistics.MethodsCount}");
        sb.AppendLine($"- **Lines of Code**: {summary.Statistics.LinesOfCode:N0}");
        sb.AppendLine();

        // 类型分布
        if (summary.Statistics.TypeDistribution.Any())
        {
            sb.AppendLine("### Type Distribution");
            sb.AppendLine();
            foreach (var kvp in summary.Statistics.TypeDistribution.OrderByDescending(x => x.Value))
            {
                sb.AppendLine($"- {kvp.Key}: {kvp.Value}");
            }

            sb.AppendLine();
        }

        // 项目详情
        sb.AppendLine("## Projects");
        sb.AppendLine();

        foreach (var project in summary.Projects.OrderBy(p => p.Name))
        {
            FormatProject(sb, project);
        }

        return sb.ToString();
    }


    public string Format(ProjectInfo summary)
    {
        var sb = new StringBuilder();

        // 头部信息
        sb.AppendLine($"# Project Summary: {summary.Name}");
        sb.AppendLine();

        // 统计概览
        sb.AppendLine("## Overview");
        sb.AppendLine();
        sb.AppendLine($"- **Files**: {summary.Statistics.FilesCount}");
        sb.AppendLine($"- **Types**: {summary.Statistics.TypesCount}");
        sb.AppendLine($"- **Methods**: {summary.Statistics.MethodsCount}");
        sb.AppendLine($"- **Lines of Code**: {summary.Statistics.LinesOfCode:N0}");
        sb.AppendLine();

        // 项目详情
        FormatProject(sb, summary);

        return sb.ToString();
    }

    private void FormatProject(StringBuilder sb, ProjectInfo project, bool isTopLevel = true)
    {
        sb.AppendLine($"### {project.Name}");
        sb.AppendLine();
        sb.AppendLine($"- **Type**: {project.OutputType}");
        sb.AppendLine($"- **Project File Path**:{project.ProjectFilePath}");
        if (isTopLevel)
        {
            sb.AppendLine($"- **Root**: `{project.FullRootDir}`");
        }
        else
        {
            sb.AppendLine($"- **Root(Relative to Solution Dir)**: `{project.RelativeRootDir}`");
        }

        if (project.TargetFrameworks.Any())
        {
            sb.AppendLine($"- **Frameworks**: {string.Join(", ", project.TargetFrameworks)}");
        }

        sb.AppendLine($"- **Language**: {project.Language ?? "C#"}");
        sb.AppendLine($"- **Language Version**: {project.LanguageVersion ?? "latest"}");
        sb.AppendLine(
            $"- **Statistics**: {project.Statistics.TypesCount} types, {project.Statistics.MethodsCount} methods");
        sb.AppendLine();

        // 依赖关系
        if (_options.IncludeDependencies)
        {
            if (project.ProjectReferences.Any())
            {
                sb.AppendLine("#### Project References");
                foreach (var pr in project.ProjectReferences)
                {
                    sb.AppendLine($"- {pr.ProjectName}");
                }

                sb.AppendLine();
            }

            if (_options.IncludePackages && project.PackageReferences.Any())
            {
                sb.AppendLine("#### Package References");
                foreach (var pkg in project.PackageReferences.Take(_options.MaxPackagesToShow))
                {
                    sb.AppendLine($"- {pkg.Name} ({pkg.Version})");
                }

                if (project.PackageReferences.Count > _options.MaxPackagesToShow)
                {
                    sb.AppendLine($"- ... and {project.PackageReferences.Count - _options.MaxPackagesToShow} more");
                }

                sb.AppendLine();
            }
        }

        // 命名空间和类型
        if (_options.IncludeTypes)
        {
            sb.AppendLine("#### Structure");
            sb.AppendLine();

            foreach (var ns in project.Namespaces.OrderBy(n => n.Name))
            {
                if (ns.Types.Count == 0) continue;

                sb.AppendLine($"- **{ns.Name}**");

                foreach (var type in ns.Types.Where(IsImportantType).Take(_options.MaxTypesPerNamespace))
                {
                    FormatType(sb, type, 2);
                }

                var remainingCount = ns.Types.Count - _options.MaxTypesPerNamespace;
                if (remainingCount > 0)
                {
                    sb.AppendLine($"  - ... and {remainingCount} more types");
                }
            }

            sb.AppendLine();
        }
    }

    private void FormatType(StringBuilder stringBuilder, TypeInfo type, int indent)
    {
        var indentStr = new string(' ', indent);

        // 基本类型信息+相对路径
        stringBuilder.Append($"{indentStr}- `{type.Name}` ({type.Kind})")
            .Append(" ")
            .Append("*")
            .Append($"@{type.RelativePath}:{type.LineNumber}")
            .Append("*");
        stringBuilder.AppendLine();
        // 摘要
        if (!string.IsNullOrEmpty(type.Summary) && _options.IncludeSummaries)
            stringBuilder.AppendLine($"{indentStr} Summary: {type.Summary}");
        stringBuilder.AppendLine();

        var typeMembers = type.Members;
        if (_options.IncludeMembers && typeMembers.Any())
        {
            var importantMembers = typeMembers
                .Where(IsImportantMember)
                .Take(_options.MaxMembersPerType);

            foreach (var member in importantMembers)
            {
                stringBuilder.AppendLine($"{indentStr}  - {member.Signature} ({member.Kind})");
            }
        }
    }

    private static bool IsImportantType(TypeInfo type)
    {
        // 公共API或标记了重要属性的类型
        return type.Accessibility == "Public" ||
               type.Attributes.Any(a => a.Contains("Controller") ||
                                        a.Contains("Service") ||
                                        a.Contains("Repository"));
    }

    private static bool IsImportantMember(MemberInfo member)
    {
        // 公共方法或属性
        return (member.Accessibility == "Public" &&
                (member.Kind == "Method" || member.Kind == "Property")) ||
               member.Attributes.Any(a => a.Contains("Route") ||
                                          a.Contains("HttpGet") ||
                                          a.Contains("HttpPost"));
    }
}