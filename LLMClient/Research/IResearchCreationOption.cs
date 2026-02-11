using LLMClient.Component.CustomControl;

namespace LLMClient.Research;

public interface IResearchCreationOption
{
    string DisplayName { get; }
    
    ThemedIcon Icon { get; }
    
    ResearchClient CreateResearchClient();
}