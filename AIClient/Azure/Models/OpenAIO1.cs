namespace LLMClient.Azure.Models;

public class OpenAIO1 : AzureModelBase
{
    public OpenAIO1(AzureClient? client, AzureModelInfo modelInfo) : base(client, modelInfo)
    {
    }

    public ulong MaxTokens { get; set; }
}