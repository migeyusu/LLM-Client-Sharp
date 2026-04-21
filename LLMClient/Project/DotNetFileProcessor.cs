using System.Diagnostics;
using Microsoft.Build.Evaluation;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LLMClient.Project;

public class AstMethodInfo
{
    public required string MethodName { get; set; }
    public required string ReturnType { get; set; }
    public List<string> Parameters { get; set; } = new();
    public List<string> Modifiers { get; set; } = new();
}

/// <summary>
/// 
/// </summary>
public static class DotNetFileProcessor
{
    /// <summary>
    /// <remarks>本方法适用于某些把powershell加入白名单的加密软件，禁止用于生产环境，后果自负</remarks>
    /// </summary>
    /// <param name="filePath"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public static string SafeReadyByPs(string filePath)
    {
        string command = $"Get-Content -Path '{filePath}'";
        var psi = new ProcessStartInfo
        {
            // Windows PowerShell 5.x
            FileName = "powershell",
            Arguments = $"-NoProfile -NonInteractive -Command \"{command}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false, // 必须为 false，才能重定向输出
            CreateNoWindow = true // 不弹出黑框
        };

        using var proc = Process.Start(psi)
                         ?? throw new InvalidOperationException("无法启动 PowerShell 进程。");

        string output = proc.StandardOutput.ReadToEnd();
        string error = proc.StandardError.ReadToEnd();

        proc.WaitForExit();

        if (proc.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"PowerShell 退出码 {proc.ExitCode}，错误信息：{error}");
        }

        return output.Trim();
    }

    public static string RemoveComments(string sourceCode, LanguageVersion languageVersion = LanguageVersion.Latest)
    {
        // 解析C#源代码
        SyntaxTree tree = CSharpSyntaxTree.ParseText(sourceCode, new CSharpParseOptions(languageVersion));
        var root = tree.GetRoot();

        // 获取所有的注释节点（包括单行和多行）
        var comments = root.DescendantTrivia()
            .Where(t => t.IsKind(SyntaxKind.SingleLineCommentTrivia) ||
                        t.IsKind(SyntaxKind.MultiLineCommentTrivia));

        // 移除所有注释
        var newRoot = root.ReplaceTrivia(comments, (_, _) => SyntaxFactory.ElasticMarker);

        // 返回无注释代码（保留原始格式）
        return newRoot.ToFullString();
    }

    // 可选：完全删除注释并重新格式化代码
    public static string RemoveCommentsAndNormalize(string sourceCode,
        LanguageVersion version = LanguageVersion.Latest)
    {
        SyntaxTree tree = CSharpSyntaxTree.ParseText(sourceCode, new CSharpParseOptions(version));
        var root = tree.GetRoot();

        var comments = root.DescendantTrivia()
            .Where(t => t.IsKind(SyntaxKind.SingleLineCommentTrivia) ||
                        t.IsKind(SyntaxKind.MultiLineCommentTrivia));
        var newRoot = root.ReplaceTrivia(comments, (_, _) => SyntaxFactory.ElasticMarker)
            .NormalizeWhitespace();
        return newRoot.ToFullString();
    }

    public static List<AstMethodInfo> ExtractValidMethods(string sourceCode,
        LanguageVersion version = LanguageVersion.Latest)
    {
        var tree = CSharpSyntaxTree.ParseText(sourceCode, new CSharpParseOptions(version));
        var root = tree.GetRoot();
        var result = new List<AstMethodInfo>();
        var classNodes = root.DescendantNodes().OfType<ClassDeclarationSyntax>();
        foreach (var classNode in classNodes)
        {
            // 普通方法
            foreach (var method in classNode.Members.OfType<MethodDeclarationSyntax>())
            {
                var info = new AstMethodInfo
                {
                    MethodName = method.Identifier.Text,
                    ReturnType = method.ReturnType?.ToString() ?? "",
                    Modifiers = method.Modifiers.Select(m => m.Text).ToList(),
                };
                if (info.Modifiers.Contains("extern"))
                {
                    continue;
                }

                if (method.ExpressionBody == null && (method.Body == null || method.Body.Statements.Count == 0))
                {
                    continue;
                }

                if (IsForwardingMethod(method))
                {
                    continue;
                }

                foreach (var p in method.ParameterList.Parameters)
                {
                    info.Parameters.Add(p.Identifier.Text);
                }

                result.Add(info);
            }
        }

        return result;
    }

    private static bool IsForwardingMethod(MethodDeclarationSyntax method)
    {
        // 尝试获取唯一的表达式（来自表达式体或单语句方法体）
        ExpressionSyntax? expression = null;
        if (method.ExpressionBody != null)
        {
            expression = method.ExpressionBody.Expression;
        }
        else if (method.Body != null && method.Body.Statements.Count == 1)
        {
            var statement = method.Body.Statements[0];
            if (statement is ReturnStatementSyntax returnStatement)
            {
                expression = returnStatement.Expression;
            }
            else if (statement is ExpressionStatementSyntax expressionStatement)
            {
                expression = expressionStatement.Expression;
            }
        }

        if (expression == null)
        {
            return false;
        }

        // 循环解开 await 表达式
        while (expression is AwaitExpressionSyntax awaitExpression)
        {
            expression = awaitExpression.Expression;
        }

        // 检查表达式是否为方法调用
        if (expression is InvocationExpressionSyntax)
        {
            return true;
        }

        // 检查表达式是否为赋值，且右侧是方法调用
        if (expression is AssignmentExpressionSyntax assignment &&
            assignment.Right is InvocationExpressionSyntax)
        {
            return true;
        }

        return false;
    }

    public static List<string> GetCsFilesFromProject(string projectFilePath, out LanguageVersion languageVersion)
    {
        if (!File.Exists(projectFilePath))
        {
            throw new FileNotFoundException($"Project file not found: {projectFilePath}");
        }

        var csFiles = new List<string>();
        // 使用 ProjectCollection 来管理项目的生命周期
        // 在 using 块中，ProjectCollection 会在结束时卸载所有项目
        using (var projectCollection = new ProjectCollection())
        {
            try
            {
                // 加载项目文件
                // 注意：对于Project类，如果项目文件本身有错误可能抛出异常
                var project = new Microsoft.Build.Evaluation.Project(projectFilePath, null, null, projectCollection);
                var langVersionString = project.GetPropertyValue("LangVersion");
                languageVersion = LanguageVersionFacts.TryParse(langVersionString, out var langVersion)
                    ? langVersion
                    : LanguageVersion.Latest;
                // 获取所有 Compile 类型的 Item
                // 这些项代表了所有需要编译的源文件，MSBuild 会自动处理通配符和隐式包含
                foreach (var item in project.GetItems("Compile"))
                {
                    // Item 的 Include 属性包含的是相对于项目文件路径的路径
                    string relativePath = item.EvaluatedInclude;

                    // 将相对路径转换为绝对路径
                    // 注意：这里需要项目文件的目录作为基准
                    string absolutePath =
                        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(projectFilePath)!, relativePath));

                    if (File.Exists(absolutePath))
                    {
                        csFiles.Add(absolutePath);
                    }
                    else
                    {
                        Console.WriteLine(
                            $"Warning: Referenced file not found on disk: {absolutePath} (from project: {projectFilePath})");
                    }
                }

                // 如果是SDK风格项目 (.NET Core / .NET 5+)，并且没有显式声明 Compile Item Group，
                // MSBuild 会默认包含 **/*.cs 文件。Project.GetItems("Compile") 会正确地返回这些文件。
                // 但为了确保万无一失且更直观地理解，我们可以考虑项目是否是SDK风格。
                // project.IsSdkStyleProject 属性可以帮助判断。
                // 一般来说，上面的 GetItems("Compile") 已经足够。
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading or processing project {projectFilePath}: {ex.Message}");
                // 根据实际需求，可以选择重新抛出异常或返回空列表
                languageVersion = LanguageVersion.Latest;
                return [];
            }
        }

        return csFiles.Distinct().ToList();
    }
}