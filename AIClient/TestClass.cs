using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using SkiaSharp;

namespace LLMClient;

public class TestClass
{
    public static async void Test()
    {
      
    }

    public static void TestSvg()
    {
    }

    // 尝试从HTML中提取嵌入的JSON数据
    private static string ExtractJsonFromHtml(string html)
    {
        try
        {
            // 尝试找到常见的JSON数据模式
            // GitHub通常会在页面中嵌入类似<script id="data" type="application/json">的数据

            // 方法1: 寻找带有type="application/json"的脚本标签
            Regex jsonScriptRegex = new Regex(@"<script[^>]*type=[""']application/json[""'][^>]*>(.*?)</script>",
                RegexOptions.Singleline);
            Match jsonMatch = jsonScriptRegex.Match(html);

            if (jsonMatch.Success)
            {
                return jsonMatch.Groups[1].Value;
            }

            // 方法2: 寻找包含marketplace数据的特定脚本标签
            Regex marketplaceDataRegex = new Regex(@"<script[^>]*id=[""']marketplace-data[""'][^>]*>(.*?)</script>",
                RegexOptions.Singleline);
            Match marketplaceMatch = marketplaceDataRegex.Match(html);

            if (marketplaceMatch.Success)
            {
                return marketplaceMatch.Groups[1].Value;
            }

            // 方法3: 搜索页面中以{ "开头和以} 结尾的JSON块
            Regex jsonBlockRegex = new Regex(@"(\{.*\bmodels\b.*\})", RegexOptions.Singleline);
            Match blockMatch = jsonBlockRegex.Match(html);

            if (blockMatch.Success)
            {
                return blockMatch.Groups[1].Value;
            }

            // 如果上述方法均失败，可以尝试寻找任何JSON对象
            Regex anyJsonRegex = new Regex(@"(\{[^{}]*\{[^{}]*\}[^{}]*\})", RegexOptions.Singleline);
            Match anyMatch = anyJsonRegex.Match(html);

            if (anyMatch.Success)
            {
                return anyMatch.Groups[1].Value;
            }

            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"提取JSON时出错: {ex.Message}");
            return null;
        }
    }
}