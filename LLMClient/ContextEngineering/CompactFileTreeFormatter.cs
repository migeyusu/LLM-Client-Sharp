using System.Text;
using LLMClient.ContextEngineering.Analysis;

namespace LLMClient.ContextEngineering;

public class CompactFileTreeFormatter
{
    // 配置：如果生成的行太长，是否强制换行（避免 Markdown 渲染问题）
    private const int MaxLineLength = 120;
    
    // 黑名单：完全不需要让 LLM 知道的文件/文件夹
    private readonly HashSet<string> _excludedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "bin", "obj", ".vs", ".git", ".idea", 
        "Properties", "AssemblyInfo.cs", "GlobalSuppressions.cs",
        "App.xaml.cs", // 通常 Agent 改 XAML 即可，除非明确需要 CodeBehind
        // "Assets", "Images" // 根据需要屏蔽资源文件夹
    };

    public string Format(ProjectInfo project)
    {
        var sb = new StringBuilder();
        // 极简头部，为了节省 Token，甚至去掉了 Project 的统计信息
        sb.AppendLine($"# Map: {project.Name}");
        
        // 1. 获取并清洗路径数据
        var files = project.Namespaces
             .SelectMany(n => n.Types)
             .Select(t => t.RelativePath)
             .Where(p => !string.IsNullOrEmpty(p))
             .Select(NormalizePath) // 统一分隔符
             .Where(p => !IsExcluded(p))
             .Distinct()
             .OrderBy(p => p)
             .ToList();

        // 2. 将 XAML 和 .cs 合并显示 (可选高级优化)
        // files = CollapseXamlPairs(files); 

        // 3. 渲染
        sb.AppendLine("```"); // 使用代码块
        var root = BuildTree(files);
        RenderCompactTree(root, "", true, sb);
        sb.AppendLine("```");
        
        return sb.ToString();
    }

    // 将路径标准化
    private string NormalizePath(string path) => path.Replace('\\', '/');

    private bool IsExcluded(string path)
    {
        var parts = path.Split('/');
        return parts.Any(p => _excludedNames.Contains(p));
    }

    private class Node
    {
        public string Name { get; set; } = string.Empty;
        public Dictionary<string, Node> Children { get; } = new();
        public bool IsFile { get; set; }
        
        // 辅助属性：判断该节点是否只包含文件子节点
        public bool ContainsOnlyFiles() => Children.Values.All(c => c.IsFile);
    }

    private Node BuildTree(List<string> paths)
    {
        var root = new Node { Name = "root" };
        foreach (var path in paths)
        {
            var parts = path.Split('/');
            var current = root;
            for (int i = 0; i < parts.Length; i++)
            {
                var part = parts[i];
                if (!current.Children.TryGetValue(part, out var next))
                {
                    next = new Node 
                    { 
                        Name = part, 
                        IsFile = (i == parts.Length - 1) 
                    };
                    current.Children[part] = next;
                }
                current = next;
            }
        }
        return root;
    }

    private void RenderCompactTree(Node node, string indent, bool isLast, StringBuilder sb)
    {
        // 根节点处理
        if (node.Name == "root")
        {
            var keys = node.Children.Keys.OrderBy(k => k).ToList();
            for (int i = 0; i < keys.Count; i++)
            {
                RenderCompactTree(node.Children[keys[i]], "", i == keys.Count - 1, sb);
            }
            return;
        }

        sb.Append(indent);
        sb.Append(isLast ? "└── " : "├── ");

        // 策略判断：
        // 如果这是一个文件夹，且它下面全是文件（或大部分是文件），尝试水平打印
        if (!node.IsFile && node.Children.Count > 0 && node.ContainsOnlyFiles())
        {
            PrintHorizontalFolder(node, sb, indent + (isLast ? "    " : "│   "));
        }
        else
        {
            // 标准垂直打印
            sb.AppendLine(node.IsFile ? node.Name : $"{node.Name}/");
            
            var children = node.Children.Values
                .OrderBy(x => x.IsFile ? 1 : 0) // 文件夹在前
                .ThenBy(x => x.Name)
                .ToList();

            for (int i = 0; i < children.Count; i++)
            {
                RenderCompactTree(children[i], indent + (isLast ? "    " : "│   "), i == children.Count - 1, sb);
            }
        }
    }

    private void PrintHorizontalFolder(Node folderNode, StringBuilder sb, string nextIndent)
    {
        // 打印文件夹名
        sb.Append($"{folderNode.Name}/");
        
        // 准备文件列表
        var fileNames = folderNode.Children.Values
            .Select(x => x.Name)
            .OrderBy(x => x)
            .ToList();

        // 格式： [File1.cs, File2.cs]
        sb.Append(" [");
        
        int currentLineLen = 0;
        bool firstCtx = true;

        for (int i = 0; i < fileNames.Count; i++)
        {
            if (!firstCtx) sb.Append(", ");
            
            var name = fileNames[i];
            
            // 简单防爆长：如果一行太长了，就换行并对齐
            if (currentLineLen + name.Length > MaxLineLength)
            {
                sb.AppendLine();
                sb.Append(nextIndent + "  "); // 手动对齐
                currentLineLen = 0;
            }
            
            sb.Append(name);
            currentLineLen += name.Length;
            firstCtx = false;
        }
        
        sb.AppendLine("]");
    }
}