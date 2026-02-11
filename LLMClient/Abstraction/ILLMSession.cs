using AutoMapper;

namespace LLMClient.Abstraction
{
    public interface ILLMSession : ICloneable
    {
        DateTime EditTime { get; }

        bool IsBusy { get; }

        Task SaveAs(string? fileName = null);

        void Delete();

        ILLMSession CloneHeader();
    }

    public interface ILLMSessionLoader<T>
        where T : class, ILLMSession
    {
        static abstract Task<T?> LoadFromStream(Stream stream, IMapper mapper);
    }
}