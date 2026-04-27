namespace LLMClient.Dialog.Models;

/// <summary>
/// 消费模式：重试时清除已有循环，继续时追加到已有循环之后。
/// </summary>
public enum ReactStepConsumeMode
{
    /// <summary>重试：消费前清除 Loops、LoopCount、CurrentStatus</summary>
    Reset,

    /// <summary>继续：不清除已有状态，循环追加到现有列表</summary>
    Append,
}