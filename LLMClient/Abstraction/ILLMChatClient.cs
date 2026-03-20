namespace LLMClient.Abstraction;

public interface ILLMChatClient : IParameterizedLLMModel, IChatEndpoint
{
    ILLMAPIEndpoint Endpoint { get; }
}