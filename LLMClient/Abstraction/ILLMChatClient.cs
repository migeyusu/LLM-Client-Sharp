namespace LLMClient.Abstraction;

public interface ILLMChatClient : IParameterizedLLMModel, IReactClient
{
    IAPIEndpoint Endpoint { get; }
}