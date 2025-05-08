using LLMClient.UI;

namespace LLMClient.Endpoints.Azure;

public class AzureOption : DefaultOption
{
    public AzureOption()
    {
        this.URL = "https://models.inference.ai.azure.com";
    }
}