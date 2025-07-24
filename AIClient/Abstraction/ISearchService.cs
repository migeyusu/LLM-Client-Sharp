using LLMClient.UI.Component;

namespace LLMClient.Abstraction;

public interface ISearchService : ICloneable
{
    string Name { get; }

    string GetUniqueId();

    ThemedIcon Icon { get; }

    bool CheckCompatible(ILLMModel model);

    Task ApplySearch(DialogContext context);
}