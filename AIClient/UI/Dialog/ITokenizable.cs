namespace LLMClient.UI.Dialog;

public interface ITokenizable
{
    /// <summary>
    /// （估计的）tokens数量
    /// </summary>
    long Tokens { get; }
}