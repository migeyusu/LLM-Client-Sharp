using System.Collections.ObjectModel;
using LLMClient.Component.ViewModel.Base;

namespace LLMClient.Dialog.Models;

/// <summary>
/// 表示一轮 ReAct 循环的 ViewModel，持有该轮的流式输出缓冲区
/// </summary>
public class ReactLoopViewModel : BaseViewModel
{
    public int LoopNumber { get; init; }

    public bool IsExpanded
    {
        get;
        set
        {
            if (value == field) return;
            field = value;
            OnPropertyChanged();
        }
    } = true;

    public bool IsCompleted
    {
        get;
        set
        {
            if (value == field) return;
            field = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// 当前轮次的流式响应缓冲区
    /// </summary>
    public ObservableCollection<string> ResponseBuffer { get; } = [];
}

