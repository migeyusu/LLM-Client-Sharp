using System.IO;
using LLMClient.Endpoints.OpenAIAPI;

namespace LLMClient.Endpoints.Converters;

public enum ModelSource
{
    Custom,
    OpenRouter,
    O3Fan,
    GeekAI,
}

public abstract class ModelMapping
{
    public static ModelMapping? Create(ModelSource source)
    {
        return source switch
        {
            ModelSource.OpenRouter => new OpenRouterModelMapping(),
            ModelSource.O3Fan => new O3FanModelMapping(),
            ModelSource.GeekAI => new GeekAIModelMapping(),
            _ => null
        };
    }

    protected ModelMapping(string name)
    {
        Name = name;
    }

    public string Name { get; }

    public abstract IList<string> AvailableModels { get; }

    public abstract Task<bool> Refresh();

    public abstract APIModelInfo? TryGet(string modelId);
}