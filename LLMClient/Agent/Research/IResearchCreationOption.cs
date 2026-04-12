using LLMClient.Component.CustomControl;

namespace LLMClient.Agent.Research;

public interface IResearchCreationOption
{
    string DisplayName { get; }
    
    ThemedIcon Icon { get; }
    
    ResearchClient CreateResearchClient();
}