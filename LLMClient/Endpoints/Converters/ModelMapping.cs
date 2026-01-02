using LLMClient.Component.ViewModel.Base;
using LLMClient.Endpoints.OpenAIAPI;

namespace LLMClient.Endpoints.Converters;

public enum ModelSource
{
    None,
    OpenRouter,
    O3Fan,
    GeekAI,
    XiaoAI,
    XiaoHuMini,
    NewAPI,
}

public abstract class ModelMapping: BaseViewModel
{
    public static ModelMapping? Create(ModelSource source)
    {
        return source switch
        {
            ModelSource.OpenRouter => new OpenRouterModelMapping(),
            ModelSource.O3Fan => new O3FanModelMapping(),
            ModelSource.GeekAI => new GeekAIModelMapping(),
            ModelSource.NewAPI => new NewAPIModelMapping(),
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

    public abstract bool MapInfo(APIModelInfo modelInfo);
}