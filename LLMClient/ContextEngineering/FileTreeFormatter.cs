using System.Text;
using LLMClient.ContextEngineering.Analysis;

namespace LLMClient.ContextEngineering;

/// <summary>
/// 生成极简的文件目录树结构，大幅节省 Tokens
/// </summary>
public class FileTreeFormatter
{
    private const int MaxDepth = 5; // 限制深度，防止过深
    private const int MaxFilesPerFolder = 30; // 限制单文件夹文件数

    public string Format(SolutionInfo solution)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# Solution Map: {solution.SolutionName}");
        
        // 1. 核心依赖概览 (只列出最重要的，不要列出几百个包)
        // 这里可以做个筛选，或者只列出 TargetFrameworks
        var allFrameworks = solution.Projects
            .SelectMany(p => p.TargetFrameworks)
            .Distinct();
        sb.AppendLine($"> Frameworks: {string.Join(", ", allFrameworks)}");
        sb.AppendLine();

        // 2. 构建所有文件的路径列表
        foreach (var project in solution.Projects.OrderBy(p => p.Name))
        {
             sb.AppendLine($"### [Project] {project.Name} ({project.OutputType})");
             // 提取该项目下的所有唯一文件路径
             // 注意：这里需要你确保 ProjectInfo 里能拿到所有 FilePath
             // 目前你的结构是 Project -> Namespaces -> Types -> FilePath
             // 我们需要把它们铺平
             
             var files = project.Namespaces
                 .SelectMany(n => n.Types)
                 .Select(t => t.RelativePath) // 假设你有 RelativePath，之前代码里看到了 extract
                 .Where(p => !string.IsNullOrEmpty(p))
                 .Distinct()
                 .OrderBy(p => p)
                 .ToList();

             sb.AppendLine("```"); // 使用代码块包裹树状图防止Markdown渲染混乱
             RenderAsciiTree(files, sb);
             sb.AppendLine("```");
             sb.AppendLine();
        }

        return sb.ToString();
    }
    
    public string Format(ProjectInfo project)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# Project Map: {project.Name}");
        
        var files = project.Namespaces
             .SelectMany(n => n.Types)
             .Select(t => t.RelativePath)
             .Where(p => !string.IsNullOrEmpty(p))
             .Distinct()
             .OrderBy(p => p)
             .ToList();

        sb.AppendLine("```");
        RenderAsciiTree(files, sb);
        sb.AppendLine("```");
        
        return sb.ToString();
    }

    private void RenderAsciiTree(List<string> filePaths, StringBuilder sb)
    {
        // 1. 将路径列表转换为树节点结构
        var root = BuildTree(filePaths);

        // 2. 递归打印
        PrintNode(root, "", true, sb);
    }

    private class Node
    {
        public string Name { get; set; } = string.Empty;
        public Dictionary<string, Node> Children { get; } = new();
        public bool IsFile { get; set; }
    }

    private Node BuildTree(List<string> paths)
    {
        var root = new Node { Name = "root" };
        foreach (var path in paths)
        {
            var parts = path.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
            var current = root;
            for (int i = 0; i < parts.Length; i++)
            {
                var part = parts[i];
                if (!current.Children.TryGetValue(part, out var next))
                {
                    next = new Node 
                    { 
                        Name = part, 
                        IsFile = (i == parts.Length - 1) // 简单假定最后一段是文件
                    };
                    current.Children[part] = next;
                }
                current = next;
            }
        }
        return root;
    }

    private void PrintNode(Node node, string indent, bool isLast, StringBuilder sb, int depth = 0)
    {
        // 根节点本身不打印，只打印子节点
        if (node.Name == "root")
        {
            var keys = node.Children.Keys.OrderBy(k => k).ToList();
            for (int i = 0; i < keys.Count; i++)
            {
                PrintNode(node.Children[keys[i]], "", i == keys.Count - 1, sb, depth + 1);
            }
            return;
        }

        sb.Append(indent);
        sb.Append(isLast ? "└── " : "├── ");
        sb.AppendLine(node.Name);

        var children = node.Children.Values.OrderBy(x => x.IsFile ? 1 : 0).ThenBy(x => x.Name).ToList(); // 文件夹在前，文件在后
        
        // 截断逻辑：如果文件太多
        if (children.Count > MaxFilesPerFolder)
        {
             var visible = children.Take(MaxFilesPerFolder).ToList();
             var invisible = children.Count - MaxFilesPerFolder;
             
             for (int i = 0; i < visible.Count; i++)
             {
                 PrintNode(visible[i], indent + (isLast ? "    " : "│   "), i == visible.Count - 1 && invisible == 0, sb, depth + 1);
             }
             
             if (invisible > 0)
             {
                 sb.Append(indent + (isLast ? "    " : "│   "));
                 sb.AppendLine($"└── ... ({invisible} more items)");
             }
        }
        else
        {
            for (int i = 0; i < children.Count; i++)
            {
                PrintNode(children[i], indent + (isLast ? "    " : "│   "), i == children.Count - 1, sb, depth + 1);
            }
        }
    }
}