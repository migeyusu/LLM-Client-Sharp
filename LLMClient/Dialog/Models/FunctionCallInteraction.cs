using Microsoft.Extensions.AI;

namespace LLMClient.Dialog.Models;

public class FunctionCallInteraction
{
    public required FunctionCallContent Call { get; init; }
    public FunctionResultContent? Result { get; init; }
}