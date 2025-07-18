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
        public IReadOnlyCollection<string> AvailableModelNames => [];
        public IReadOnlyCollection<ILLMModel> AvailableModels => [];

        public ILLMClient? NewClient(string modelName)
        {
            throw new NotImplementedException();
        }

        public ILLMClient? NewClient(ILLMModel model)
        {
            throw new NotImplementedException();
        }

        public ILLMModel? GetModel(string modelName)
        {
            throw new NotImplementedException();
        }

        public Task InitializeAsync()
        {
            throw new NotImplementedException();
        }
    }
}