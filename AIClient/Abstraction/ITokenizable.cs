namespace LLMClient.Abstraction;

public interface ITokenizable
{
    /// <summary>
    /// （估计的）tokens数量
    /// </summary>
    long Tokens { get; }
}