using System.Windows.Media;
using LLMClient.Abstraction;
using LLMClient.Data;

namespace LLMClient.Endpoints
{
    public class NullLLMEndpoint : ILLMEndpoint
    {
        public static NullLLMEndpoint Instance { get; } = new NullLLMEndpoint();
        public string DisplayName => "Null Endpoint";
        public bool IsInbuilt => false;
        public bool IsEnabled => true;
        public string Name => "NullEndpoint";
        public ImageSource Icon => ImageExtensions.EndpointIcon;

        public IReadOnlyCollection<ILLMChatModel> AvailableModels => [];

        public ILLMChatClient? NewChatClient(string modelName)
        {
            return NullLlmModelClient.Instance;
        }

        public ILLMChatClient? NewChatClient(ILLMChatModel model)
        {
            return NullLlmModelClient.Instance;
        }

        public ILLMChatModel? GetModel(string modelName)
        {
            return NullLlmModelClient.Instance.Model;
        }

        public Task InitializeAsync()
        {
            return Task.CompletedTask;
        }
    }
}