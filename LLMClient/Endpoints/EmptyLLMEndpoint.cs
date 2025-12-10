using System.Windows.Media;
using LLMClient.Abstraction;
using LLMClient.Data;

namespace LLMClient.Endpoints
{
    public class EmptyLLMEndpoint : ILLMAPIEndpoint
    {
        public static EmptyLLMEndpoint Instance { get; } = new EmptyLLMEndpoint();
        public string DisplayName => "Empty Endpoint";
        public bool IsInbuilt => false;
        public bool IsEnabled => true;
        public string Name => "EmptyEndpoint";
        public ImageSource Icon => ImageExtensions.EndpointIcon;

        public IReadOnlyCollection<ILLMChatModel> AvailableModels => [];

        public ILLMChatClient? NewChatClient(ILLMChatModel model)
        {
            return null;
        }

        public ILLMChatModel? GetModel(string modelName)
        {
            return null;
        }

        public Task InitializeAsync()
        {
            return Task.CompletedTask;
        }
    }
}