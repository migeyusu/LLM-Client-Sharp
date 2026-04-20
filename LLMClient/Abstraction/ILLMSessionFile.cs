namespace LLMClient.Abstraction;

public interface ILLMSessionFile : ICloneable
{
    bool IsBusy { get; }
    ILLMSessionFile CloneHeader();

    Task SaveAs(string? fileName = null);

    void Delete();

    DateTime EditTime { get; }
}