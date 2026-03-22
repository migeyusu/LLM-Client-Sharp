using System.Net.Http;
using System.Text;
using System.Text.Json.Nodes;

namespace LLMClient.Endpoints;

public class OpenAICompatabilityHandler : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (request.Content != null && request.Content.Headers.ContentType?.MediaType == "application/json")
        {
            var jsonString = await request.Content.ReadAsStringAsync(cancellationToken);

            // 只有发现疑似需要清洗的数组特征时才启动解析，降低性能损耗
            if (jsonString.Contains("\"type\":[\"") || jsonString.Contains("\"type\": [\""))
            {
                var rootNode = JsonNode.Parse(jsonString);
                if (rootNode != null)
                {
                    CleanTypeArrays(rootNode);
                }

                // 将清理后的JSON重新写回请求内容中
                request.Content = new StringContent(rootNode!.ToJsonString(), Encoding.UTF8, "application/json");
            }
        }

        return await base.SendAsync(request, cancellationToken);
    }

    /// <summary>
    /// 递归遍历 JSON 节点，将 "type": ["xxx", "null"] 收缩为 "type": "xxx"
    /// </summary>
    private void CleanTypeArrays(JsonNode node)
    {
        if (node is JsonObject jsonObject)
        {
            // 如果碰巧这个对象有个 key 叫做 "type"，且它的 value 是数组
            if (jsonObject.TryGetPropertyValue("type", out var typeNode) && typeNode is JsonArray typeArray)
            {
                // 找到其中不是 "null" 的元素作为主类型
                string mainType = "string"; // fallback
                foreach (var item in typeArray)
                {
                    var typeValue = item?.GetValue<string>();
                    if (!string.Equals(typeValue, "null", StringComparison.OrdinalIgnoreCase))
                    {
                        mainType = typeValue;
                        break;
                    }
                }

                // 直接替换掉数组，变成简单的字符串
                jsonObject["type"] = mainType;
            }

            // 递归清理其他子属性
            foreach (var kvp in jsonObject)
            {
                if (kvp.Value != null)
                {
                    CleanTypeArrays(kvp.Value);
                }
            }
        }
        else if (node is JsonArray jsonArray)
        {
            // 对于数组继续往下递归
            foreach (var item in jsonArray)
            {
                if (item != null)
                {
                    CleanTypeArrays(item);
                }
            }
        }
    }
}