using System.Windows.Media;
using LLMClient.Abstraction;
using LLMClient.Data;

namespace LLMClient.Endpoints
{
    public class EmptyLLMEndpoint : ILLMEndpoint
    {
        public static EmptyLLMEndpoint Instance { get; } = new EmptyLLMEndpoint();
        public string DisplayName => "Null Endpoint";
        public bool IsInbuilt => false;
        public bool IsEnabled => true;
        public string Name => "NullEndpoint";
        public ImageSource Icon => ImageExtensions.EndpointIcon;

        public IReadOnlyCollection<ILLMChatModel> AvailableModels => [];

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