namespace LLMClient.Azure.Models;

public class Gpt4OMini : Gpt4o
{
    public Gpt4OMini(AzureClient? client, AzureModelInfo modelInfo) : base(client, modelInfo)
    {
    }
}