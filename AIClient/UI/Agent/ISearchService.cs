using LLMClient.Abstraction;

namespace LLMClient.UI.Agent;

/**
 * search agent 会自动根据内容产生搜索请求，并将结果添加到对话中。
 */
public class SearchAgent: IAgent, ISearchService
{
    public bool CheckAvailable(ILLMModel model)
    {
        return true;
    }

    public Task ApplySearch(DialogContext context)
    {
        throw new NotSupportedException();
    }
}