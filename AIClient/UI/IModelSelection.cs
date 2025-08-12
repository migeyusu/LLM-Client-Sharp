using System.Windows.Input;
using LLMClient.Abstraction;

namespace LLMClient.UI;

public interface IModelSelection
{
    IEndpointService EndpointService { get; }

    ILLMChatModel? SelectedModel { get; set; }

    ICommand? SubmitCommand { get; }
}