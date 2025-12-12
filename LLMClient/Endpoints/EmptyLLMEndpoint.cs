using System.Windows.Media;
using LLMClient.Abstraction;
using LLMClient.Component.CustomControl;
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
        public ThemedIcon Icon => ImageExtensions.EndpointThemedIcon;

        public IReadOnlyCollection<ILLMModel> AvailableModels => [];

        public ILLMChatClient? NewChatClient(ILLMModel model)
        {
            return null;
        }

        public ILLMModel? GetModel(string modelName)
        {
            return null;
        }

        public Task InitializeAsync()
        {
            return Task.CompletedTask;
        }
    }
}