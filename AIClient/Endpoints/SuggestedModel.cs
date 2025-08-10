using LLMClient.Abstraction;

namespace LLMClient.Endpoints;

public class SuggestedModel
{
    public SuggestedModel(ILLMModel llmModel)
    {
        Endpoint = llmModel.Endpoint!;
        LlmModel = llmModel;
    }

    public ILLMEndpoint Endpoint { get; set; }

    public ILLMModel LlmModel { get; set; }
}