using System.Windows;

namespace LLMClient.UI.Render;

public class HtmlViewContext
{
    public required string HtmlContent { get; set; }

    public static ComponentResourceKey HtmlViewContextKey { get; } =
        new ComponentResourceKey(typeof(HtmlViewContext), nameof(HtmlViewContextKey));
}

/*var code = content.TrimStart("cpp".ToCharArray()).TrimStart('\n');
        // 创建 ANTLR 输入流
        AntlrInputStream inputStream = new AntlrInputStream(codetest);

        // 使用生成的 CSharpLexer 分析词法
        CPP14Lexer lexer = new CPP14Lexer(inputStream);
        CommonTokenStream tokenStream = new CommonTokenStream(lexer);

        // 遍历所有 Token
        tokenStream.Fill();
        var enumerable = tokenStream.GetTokens();
        var enumerableCount = enumerable.Count;
        foreach (var token in enumerable)
        {
            // 根据 Token 类型输出不同颜色
            /*string highlighted = HighlightToken(token);
            Console.Write(highlighted);* /
        }*/
/*string HighlightToken(IToken token)
{
    // 获取 Token 类型
    string tokenText = token.Text;
    int tokenType = token.Type;
    Console.WriteLine(tokenType);
    // 根据 Token 类型添加样式
    switch (tokenType)
    {
        case CPP14Lexer.Class: // 这是 C# 的关键字
            return $"<span style='color: blue;'>{tokenText}</span>";
        case CPP14Lexer.And: // 字符串
            return $"<span style='color: green;'>{tokenText}</span>";
        case CPP14Lexer.Catch: // 注释
            return $"<span style='color: grey;'>{tokenText}</span>";
        default: // 其他类型，默认不加样式
            return tokenText;
    }
}*/