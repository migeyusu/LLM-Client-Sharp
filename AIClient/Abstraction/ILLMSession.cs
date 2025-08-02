namespace LLMClient.Abstraction
{
    public interface ILLMSession : ICloneable
    {
        DateTime EditTime { get; }

        bool IsBusy { get; }

        Task Backup();

        void Delete();
    }
}