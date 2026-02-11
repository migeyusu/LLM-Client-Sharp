using System.Diagnostics;
using System.Text.Json.Nodes;

namespace LLMClient.Endpoints;

public static class EndPointsConfig
{
    /* format:
     * EndPoints:{
     * Github Copilot:{}
     * OpenAIAPICompatible:[{},{}]
     * OtherKey:{}
     * }
     */

    public const string EndPointsJsonFileName = "EndPoints.json";

    /// <summary>
    /// 保存自定义终结点
    /// </summary>
    public const string EndpointsNodeName = "Endpoints";

    /// <summary>
    /// 保存配置节点
    /// </summary>
    public const string OptionsNodeName = "Options";

    /// <summary>
    /// 加载总节点
    /// </summary>
    /// <returns></returns>
    public static async Task<JsonNode> LoadOrCreateRoot()
    {
        var fullPath = Path.GetFullPath(EndPointsJsonFileName);
        var node = await TryLoadDoc(fullPath);
        return node ?? JsonNode.Parse("""{}""")!;
    }

    public static async Task<JsonNode?> TryLoadDoc(string fullPath)
    {
        var fileInfo = new FileInfo(fullPath);
        if (fileInfo.Exists)
        {
            try
            {
                await using (var fileStream = fileInfo.OpenRead())
                {
                    var jsonNode = await JsonNode.ParseAsync(fileStream);
                    if (jsonNode != null)
                    {
                        return jsonNode;
                    }
                }
            }
            catch (Exception e)
            {
                Trace.TraceError(e.Message);
            }
        }

        return null;
    }

    public static async Task<JsonNode> LoadOptionNode()
    {
        var loadEndpointsDoc = await LoadOrCreateRoot();
        return loadEndpointsDoc.GetOrCreate(OptionsNodeName);
    }

    public static async Task<JsonNode> LoadEndpointsNode()
    {
        var loadEndpointsDoc = await LoadOrCreateRoot();
        return loadEndpointsDoc.GetOrCreate(EndpointsNodeName);
    }

    /// <summary>
    /// 保存总节点
    /// </summary>
    /// <param name="node"></param>
    public static Task SaveDoc(JsonNode node)
    {
        var fullPath = Path.GetFullPath(EndPointsJsonFileName);
        return node.SaveJsonToFileAsync(fullPath, Extension.DefaultJsonSerializerOptions);
    }
}