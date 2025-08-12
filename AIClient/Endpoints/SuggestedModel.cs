using LLMClient.Abstraction;

namespace LLMClient.Endpoints;

public class SuggestedModel
{
    public SuggestedModel(ILLMChatModel llmModel)
    {
        Endpoint = llmModel.Endpoint!;
        LlmModel = llmModel;
    }

    public ILLMEndpoint Endpoint { get; set; }

    public ILLMChatModel LlmModel { get; set; }
}