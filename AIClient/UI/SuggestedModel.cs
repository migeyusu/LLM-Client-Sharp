using LLMClient.Abstraction;

namespace LLMClient.UI;

public class SuggestedModel
{
    public SuggestedModel(ILLMEndpoint endpoint, ILLMModel llmModel)
    {
        Endpoint = endpoint;
        LlmModel = llmModel;
    }

    public ILLMEndpoint Endpoint { get; set; }

    public ILLMModel LlmModel { get; set; }
}