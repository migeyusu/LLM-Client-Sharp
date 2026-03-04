using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace LLMClient.ContextEngineering.Analysis;

public static class RoslynExtensions
{
    public static string GetSymbolId(this ISymbol symbol)
    {
        return symbol.GetDocumentationCommentId() ?? symbol.ToDisplayString();
    }
    
    private const string SummaryTag = "summary";

    public static string GetXmlComment(this SyntaxNode node)
    {
        if (!node.HasLeadingTrivia)
        {
            return string.Empty;
        }

        foreach (var trivia in node.GetLeadingTrivia())
        {
            if (!trivia.HasStructure) continue;

            if (trivia.GetStructure() is not DocumentationCommentTriviaSyntax docComment) continue;

            foreach (var nodeInDoc in docComment.Content)
            {
                if (nodeInDoc is not XmlElementSyntax xmlElement) continue;

                if (xmlElement.StartTag.Name.LocalName.Text != SummaryTag) continue;

                var sb = new System.Text.StringBuilder();
                foreach (var content in xmlElement.Content)
                {
                    if (content is XmlTextSyntax xmlText)
                    {
                        foreach (var token in xmlText.TextTokens)
                        {
                            var text = token.ValueText;
                            if (!string.IsNullOrWhiteSpace(text))
                            {
                                if (sb.Length > 0) sb.Append(' ');
                                sb.Append(text.Trim());
                            }
                        }
                    }
                }

                return sb.ToString();
            }
        }

        return string.Empty;
    }
    

    public static List<string> ExtractAttributes(this ISymbol symbol)  
    {  
        var attributes = new List<string>();  
  
        foreach (var attr in symbol.GetAttributes())  
        {  
            if (attr.AttributeClass != null)  
            {  
                var name = attr.AttributeClass.Name;  
                // 移除 "Attribute" 后缀  
                if (name.EndsWith("Attribute"))  
                    name = name.Substring(0, name.Length - 9);  
                attributes.Add(name);  
            }  
        }  
  
        return attributes;  
    }


    public static List<CodeLocation> GetLocations(this ISymbol symbol)
    {
        return symbol.Locations
            .Where(loc => loc.IsInSource)
            .Select(loc =>
            {
                var lineSpan = loc.GetLineSpan(); // 获取文件与行列范围  
                return new CodeLocation
                {
                    FilePath = lineSpan.Path,
                    Location = new LinePositionSpan(
                        new LinePosition(
                            lineSpan.StartLinePosition.Line + 1, // 转为1基  
                            lineSpan.StartLinePosition.Character + 1
                        ),
                        new LinePosition(
                            lineSpan.EndLinePosition.Line + 1,
                            lineSpan.EndLinePosition.Character + 1
                        )
                    )
                };
            })
            .ToList();
    }

    public static string BuildSymbolSignature(this ISymbol symbol)
    {
        // 定义显示格式：这是关键
        var format = new SymbolDisplayFormat(
            globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
            typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypes,
            genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
            memberOptions: SymbolDisplayMemberOptions.IncludeParameters |
                           SymbolDisplayMemberOptions.IncludeType |
                           SymbolDisplayMemberOptions.IncludeModifiers |
                           SymbolDisplayMemberOptions.IncludeRef,
            parameterOptions: SymbolDisplayParameterOptions.IncludeType);

        // 示例输出: "public async Task<int> MyMethod(string name, int count = 0)"
        return symbol.ToDisplayString(format);
        /*var sb = new StringBuilder();
        if (symbol.ReturnsVoid)
            sb.Append("void");
        else
            sb.Append(FormatTypeName(symbol.ReturnType));

        sb.Append(' ');
        sb.Append(symbol.Name);

        if (symbol.IsGenericMethod)
        {
            sb.Append('<');
            sb.Append(string.Join(", ", symbol.TypeParameters.Select(t => t.Name)));
            sb.Append('>');
        }

        sb.Append('(');
        sb.Append(string.Join(", ", symbol.Parameters.Select(p =>
            $"{FormatTypeName(p.Type)} {p.Name}")));
        sb.Append(')');

        return sb.ToString();*/
    }
}