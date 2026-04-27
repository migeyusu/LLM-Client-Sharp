namespace LLMClient.Agent;

public interface IInbuiltAgent
{
    private static readonly Lazy<Type[]> ChildTypesLazy = new(() => typeof(IInbuiltAgent).ImplementsTypes().ToArray());

    static Type[] ChildTypes => ChildTypesLazy.Value;
}