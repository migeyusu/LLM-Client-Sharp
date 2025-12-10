using System.IO;
using System.Text;
using LLMClient.Component.Render;
using Markdig.Extensions.JiraLinks;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace LLMClient.Rag.Document;

public class MarkdownParser
{
    public async Task<List<MarkdownNode>> Parse(string markdownFilePath)
    {
        //1. 解析文件
        var markdownText = await File.ReadAllTextAsync(markdownFilePath);
        if (!RendererExtensions.TryParseMarkdown(markdownText, out var markdownDocument))
        {
            throw new Exception("Invalid markdown file");
        }

        var rootNodes = new List<MarkdownNode>();
        // 使用一个栈来帮助我们维护当前的层级关系
        var parentStack = new Stack<MarkdownNode>();

        // 2. 遍历AST的所有块
        foreach (var block in markdownDocument)
        {
            if (block is HeadingBlock headingBlock)
            {
                // 从HeadingBlock中提取纯文本标题
                var title = GetPlainText(headingBlock.Inline);
                if (string.IsNullOrEmpty(title))
                {
                    var headingBlockSpan = headingBlock.Span;
                    title = markdownText.Substring(headingBlockSpan.Start, headingBlockSpan.Length);
                }

                var newNode = new MarkdownNode(title, headingBlock.Level);
                // 3. 根据标题级别调整其在树中的位置
                while (parentStack.Count > 0 && parentStack.Peek().Level >= newNode.Level)
                {
                    parentStack.Pop();
                }

                if (parentStack.Count > 0)
                {
                    parentStack.Peek().Children.Add(newNode);
                }
                else
                {
                    rootNodes.Add(newNode);
                }

                parentStack.Push(newNode);
            }
            else if (parentStack.Count > 0)
            {
                // 4. 将非标题内容块追加到当前标题节点的Content中
                var currentTop = parentStack.Peek();
                currentTop.ContentUnits.Add(new MarkdownText(block, markdownText, markdownFilePath));
            }
        }

        foreach (var root in rootNodes)
        {
            SetLevels(root, 0);
        }

        return rootNodes;

        //重新设定level属性，要求根节点为0
        void SetLevels(MarkdownNode node, int level)
        {
            node.Level = level;
            foreach (var child in node.Children)
            {
                SetLevels(child, level + 1);
            }
        }
    }

    /// <summary>
    /// 辅助方法：从Markdig的Inline元素中提取纯文本。
    /// </summary>
    private string GetPlainText(ContainerInline? containerInline)
    {
        if (containerInline == null) return string.Empty;
        var stringBuilder = new StringBuilder();
        GetPlainTextRecursive(stringBuilder, containerInline);
        return stringBuilder.ToString();
    }

    private void GetPlainTextRecursive(StringBuilder stringBuilder, IEnumerable<Inline> inlines)
    {
        foreach (var inline in inlines)
        {
            if (inline is LiteralInline literal)
            {
                stringBuilder.Append(literal.Content);
            }
            else if (inline is CodeInline code)
            {
                stringBuilder.Append(code.Content);
            }
            else if (inline is JiraLink jiraLink)
            {
                stringBuilder.Append($"{jiraLink.ProjectKey}-{jiraLink.Issue}");
            }
            else if (inline is LinkInline linkInline)
            {
                stringBuilder.Append(linkInline.Title);
            }
            else if (inline is EmphasisInline emphasis)
            {
                GetPlainTextRecursive(stringBuilder, emphasis);
            }
        }
    }
}