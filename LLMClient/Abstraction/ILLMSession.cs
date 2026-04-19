using AutoMapper;

namespace LLMClient.Abstraction
{
    public interface ILLMSession : ICloneable
    {
        bool IsBusy { get; }
        ILLMSession CloneHeader();

        IEnumerable<Type> SupportedAgents { get; }
    }

    public interface ILLMSessionFile : ILLMSession
    {
        Task SaveAs(string? fileName = null);

        void Delete();

        DateTime EditTime { get; }
    }

    public interface ILoadableLLMSession<T>
        where T : class, ILLMSessionFile
    {
        static abstract Task<T?> LoadFromStream(Stream stream, IMapper mapper);
    }
}