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
        set => SetField(ref field, value);
    } = true;

    public bool IsCompleted
    {
        get;
        set => SetField(ref field, value);
    }

    /// <summary>
    /// 当前轮次的流式响应缓冲区
    /// </summary>
    public ObservableCollection<string> ResponseBuffer { get; } = [];

    /// <summary>
    /// 上下文用量视图模型
    /// </summary>
    public ContextUsageViewModel? ContextUsage
    {
        get;
        set
        {
            if (Equals(value, field)) return;
            field = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// 该轮输出的首行文本（从 ResponseBuffer 动态获取）
    /// </summary>
    public string? FirstLine { get; set; }

    /// <summary>
    /// 该轮的延迟（毫秒）
    /// </summary>
    public int LatencyMs { get; set; }

    public void NotifyFirstLine()
    {
        if (!string.IsNullOrEmpty(FirstLine))
        {
            return;
        }

        if (!ResponseBuffer.Any())
        {
            return;
        }

        var firstLine = ResponseBuffer
            .SelectMany(line => line.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
            .FirstOrDefault(line => !string.IsNullOrWhiteSpace(line))?.Trim();
        if (string.IsNullOrEmpty(firstLine))
        {
            return;
        }

        this.FirstLine = firstLine;
        OnPropertyChanged(nameof(FirstLine));
    }
}