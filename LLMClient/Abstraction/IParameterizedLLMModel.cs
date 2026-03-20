namespace LLMClient.Abstraction;

public interface IParameterizedLLMModel
{
    IEndpointModel Model { get; }

    IModelParams Parameters { get; set; }
}