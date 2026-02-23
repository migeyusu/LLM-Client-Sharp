using System.Text;
using LLMClient.ContextEngineering.Analysis;

namespace LLMClient.ContextEngineering.Prompt;

/// <summary>
/// 将文件路径列表渲染为 ASCII 目录树。
/// 核心方法接受已计算好的相对路径集合，Solution/Project 重载负责路径提取。
/// </summary>
public class FileTreeFormatter
{
    private const int DefaultMaxFilesPerFolder = 30;

    // ── 核心方法：从外部传入已相对化的路径列表 ──────────────────────────

    /// <param name="header">树顶部的标题行，为空则不输出</param>
    /// <param name="relativePaths">相对于某个基准目录的路径集合</param>
    /// <param name="maxDepth">最大渲染深度，超出时折叠并注明</param>
    /// <param name="excludePatterns">路径片段黑名单（大小写不敏感子串匹配）</param>
    /// <param name="maxFilesPerFolder">每级目录最多显示的条目数</param>
    public string Format(
        string header,
        IEnumerable<string> relativePaths,
        int maxDepth = 5,
        ICollection<string>? excludePatterns = null,
        int maxFilesPerFolder = DefaultMaxFilesPerFolder)
    {
        var sb = new StringBuilder();

        if (!string.IsNullOrWhiteSpace(header))
            sb.AppendLine(header).AppendLine();

        sb.AppendLine("```");

        var filtered = relativePaths
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Where(p => excludePatterns == null || !excludePatterns.Any(ep =>
                p.Contains(ep, StringComparison.OrdinalIgnoreCase)))
            .Distinct()
            .OrderBy(p => p)
            .ToList();

        var root = BuildTree(filtered);
        PrintNode(root, "", true, sb, currentDepth: 0, maxDepth, maxFilesPerFolder);

        sb.AppendLine("```");
        return sb.ToString();
    }

    // ── Solution 重载：每个 Project 单独一节，路径相对于 Solution 根目录 ──

    public string Format(
        SolutionInfo solution,
        int maxDepth = 5,
        ICollection<string>? excludePatterns = null)
    {
        var solutionDir = Path.GetDirectoryName(solution.SolutionPath) ?? string.Empty;
        var sb = new StringBuilder();

        sb.AppendLine($"# Solution Map: {solution.SolutionName}");

        var allFrameworks = solution.Projects
            .SelectMany(p => p.TargetFrameworks)
            .Distinct()
            .OrderBy(x => x);
        sb.AppendLine($"> Frameworks: {string.Join(", ", allFrameworks)}");
        sb.AppendLine();

        foreach (var project in solution.Projects.OrderBy(p => p.Name))
        {
            sb.AppendLine($"### [Project] {project.Name} ({project.OutputType})");
            var paths = ExtractRelativePaths(project, solutionDir);
            sb.Append(Format(string.Empty, paths, maxDepth, excludePatterns));
            sb.AppendLine();
        }

        return sb.ToString();
    }

    // ── Project 重载：路径相对于 Project 根目录 ─────────────────────────

    public string Format(
        ProjectInfo project,
        int maxDepth = 5,
        ICollection<string>? excludePatterns = null)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# Project Map: {project.Name}");
        var paths = ExtractRelativePaths(project, project.FullRootDir);
        sb.Append(Format(string.Empty, paths, maxDepth, excludePatterns));
        return sb.ToString();
    }

    // ── 路径提取（优先 Files 索引，回退到 Type 位置）──────────────────────

    private static IEnumerable<string> ExtractRelativePaths(ProjectInfo project, string baseDir)
    {
        if (project.Files.Count > 0)
        {
            return project.Files
                .Select(f => Path.GetRelativePath(baseDir, f.FilePath))
                .Where(p => !string.IsNullOrWhiteSpace(p) && p != ".");
        }

        // Fallback：从 Type 的 RelativePath 推算（旧行为兼容）
        return project.Namespaces
            .SelectMany(n => n.Types)
            .Select(t =>
            {
                var absPath = Path.IsPathRooted(t.RelativePath)
                    ? t.RelativePath
                    : Path.Combine(project.FullRootDir, t.RelativePath);
                return Path.GetRelativePath(baseDir, absPath);
            })
            .Where(p => !string.IsNullOrWhiteSpace(p) && p != ".")
            .Distinct();
    }

    // ── 树构建 ────────────────────────────────────────────────────────────

    private sealed class TreeNode
    {
        public string Name { get; init; } = string.Empty;
        public Dictionary<string, TreeNode> Children { get; } = new(StringComparer.OrdinalIgnoreCase);
        public bool IsFile { get; init; }
    }

    private static TreeNode BuildTree(IEnumerable<string> paths)
    {
        var root = new TreeNode { Name = "__root__" };

        foreach (var path in paths)
        {
            var parts = path.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
            var current = root;

            for (var i = 0; i < parts.Length; i++)
            {
                var part = parts[i];
                if (!current.Children.TryGetValue(part, out var next))
                {
                    next = new TreeNode { Name = part, IsFile = i == parts.Length - 1 };
                    current.Children[part] = next;
                }

                current = next;
            }
        }

        return root;
    }

    // ── 树打印（深度限制 + 条目截断）────────────────────────────────────

    private static void PrintNode(
        TreeNode node,
        string indent,
        bool isLast,
        StringBuilder sb,
        int currentDepth,
        int maxDepth,
        int maxFilesPerFolder)
    {
        // ── 根节点：不打印自身，但同样应用排序与截断 ───────────────────────
        if (node.Name == "__root__")
        {
            var rootChildren = node.Children.Values
                .OrderBy(c => c.IsFile ? 1 : 0) // Fix 1：目录优先
                .ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (rootChildren.Count > maxFilesPerFolder) // Fix 2：根节点截断
            {
                var visible = rootChildren.Take(maxFilesPerFolder).ToList();
                var hiddenCount = rootChildren.Count - maxFilesPerFolder;

                for (var i = 0; i < visible.Count; i++)
                    PrintNode(visible[i], "", i == visible.Count - 1 && hiddenCount == 0,
                        sb, currentDepth, maxDepth, maxFilesPerFolder);

                if (hiddenCount > 0)
                    sb.AppendLine($"└── ... ({hiddenCount} more items)");
            }
            else
            {
                for (var i = 0; i < rootChildren.Count; i++)
                    PrintNode(rootChildren[i], "", i == rootChildren.Count - 1,
                        sb, currentDepth, maxDepth, maxFilesPerFolder);
            }

            return;
        }

        // ── 普通节点 ──────────────────────────────────────────────────────
        sb.Append(indent).Append(isLast ? "└── " : "├── ").AppendLine(node.Name);

        if (node.Children.Count == 0) return;

        var childIndent = indent + (isLast ? "    " : "│   ");

        // Fix 3：改为 >= maxDepth，让第 maxDepth 层节点仍可打印自身，
        //         仅折叠其子节点（即第 maxDepth+1 层）
        if (currentDepth >= maxDepth)
        {
            sb.Append(childIndent)
                .AppendLine($"└── ... ({node.Children.Count} items, depth limit {maxDepth})");
            return;
        }

        var children = node.Children.Values
            .OrderBy(c => c.IsFile ? 1 : 0)
            .ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (children.Count > maxFilesPerFolder)
        {
            var visible = children.Take(maxFilesPerFolder).ToList();
            var hiddenCount = children.Count - maxFilesPerFolder;

            for (var i = 0; i < visible.Count; i++)
                PrintNode(visible[i], childIndent, i == visible.Count - 1 && hiddenCount == 0,
                    sb, currentDepth + 1, maxDepth, maxFilesPerFolder);

            if (hiddenCount > 0)
                sb.Append(childIndent).AppendLine($"└── ... ({hiddenCount} more items)");
        }
        else
        {
            for (var i = 0; i < children.Count; i++)
                PrintNode(children[i], childIndent, i == children.Count - 1,
                    sb, currentDepth + 1, maxDepth, maxFilesPerFolder);
        }
    }
}