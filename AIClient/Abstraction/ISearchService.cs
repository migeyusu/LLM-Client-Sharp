namespace LLMClient.Abstraction;

public interface ISearchService
{
    bool CheckAvailable(ILLMModel model);
    
    Task ApplySearch(DialogContext context);
}

