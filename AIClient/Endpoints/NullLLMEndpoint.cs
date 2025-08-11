using System.Windows.Media;
using LLMClient.Abstraction;
using LLMClient.Data;

namespace LLMClient.Endpoints
{
    public class NullLLMEndpoint : ILLMEndpoint
    {
        public static NullLLMEndpoint Instance { get; } = new NullLLMEndpoint();
        public string DisplayName { get; } = "Null Endpoint";
        public bool IsDefault { get; } = false;

        public bool IsEnabled { get; } = false;
        public string Name { get; } = "NullEndpoint";
        public ImageSource Icon => ImageExtensions.EndpointIcon;
        
        public IReadOnlyCollection<ILLMModel> AvailableModels => [];

        public ILLMChatClient? NewClient(string modelName)
        {
            throw new NotSupportedException();
        }

        public ILLMChatClient? NewClient(ILLMModel model)
        {
            throw new NotSupportedException();
        }

        public ILLMModel? GetModel(string modelName)
        {
            throw new NotSupportedException();
        }

        public Task InitializeAsync()
        {
            throw new NotSupportedException();
        }
    }
}