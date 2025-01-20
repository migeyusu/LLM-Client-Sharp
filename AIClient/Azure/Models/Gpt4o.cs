namespace LLMClient.Azure.Models;

public class Gpt4o : AzureModelBase
{
    public Gpt4o(AzureClient? client, AzureModelInfo modelInfo) : base(client, modelInfo)
    {
    }

    /// <summary>
    /// 通过选择最可能的单词来控制文本多样性，直到达到规定的概率。
    /// </summary>
    public float TopP { get; set; }

    /// <summary>
    /// 控制响应中的随机性，使用较低的值以获得更确定性。
    /// </summary>
    public float Temperature { get; set; }

    public ulong MaxTokens { get; set; }
    
}