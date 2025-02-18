using LLMClient.Azure.Models;
using Microsoft.Extensions.Configuration;

namespace LLMClient.Azure;

public abstract class ModelCreation
{
    public abstract AzureModelBase? CreateModel(GithubCopilotEndPoint endPoint, AzureModelInfo modelInfo);
}

public class ModelCreation<T> : ModelCreation where T : AzureModelBase
{
    public ModelCreation()
    {
    }

    public ModelCreation(Action<T> initial)
    {
        Initial = initial;
    }

    private Action<T>? Initial { get; }

    public override AzureModelBase? CreateModel(GithubCopilotEndPoint endPoint, AzureModelInfo modelInfo)
    {
        var instance = Activator.CreateInstance(typeof(T), endPoint, modelInfo);
        if (instance is T model)
        {
            Initial?.Invoke(model);
            return model;
        }

        return null;
    }
}