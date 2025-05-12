using LLMClient.UI;

namespace LLMClient.Endpoints.Azure;

public class AzureOption : DefaultOption
{
    public AzureOption()
    {
        this.URL = "https://models.github.ai/inference"; //"https://models.inference.ai.azure.com";
    }
}