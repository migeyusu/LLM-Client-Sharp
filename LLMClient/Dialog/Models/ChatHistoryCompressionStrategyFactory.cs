using LLMClient.Abstraction;
using LLMClient.Component.ViewModel;
using LLMClient.Dialog.Proc;

namespace LLMClient.Dialog.Models;

public class ChatHistoryCompressionStrategyFactory
{
    private readonly IViewModelFactory _viewModelFactory;

    public ChatHistoryCompressionStrategyFactory(IViewModelFactory viewModelFactory)
    {
        _viewModelFactory = viewModelFactory;
    }

    public IChatHistoryCompressionStrategy? Create(ReactHistoryCompressionMode mode)
    {
        return mode switch
        {
            ReactHistoryCompressionMode.TaskSummary => _viewModelFactory
                .Create<TaskSummaryChatHistoryCompressionStrategy>(),
            ReactHistoryCompressionMode.ObservationMasking => _viewModelFactory
                .Create<ObservationMaskingChatHistoryCompressionStrategy>(),
            ReactHistoryCompressionMode.LoopSummary => _viewModelFactory
                .Create<LoopSummaryChatHistoryCompressionStrategy>(),
            _ => null,
        };
    }
}