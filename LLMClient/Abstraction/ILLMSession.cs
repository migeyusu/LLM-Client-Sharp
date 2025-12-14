using System.IO;
using AutoMapper;

namespace LLMClient.Abstraction
{
    public interface ILLMSession : ICloneable
    {
        DateTime EditTime { get; }

        bool IsBusy { get; }

        Task Backup();

        void Delete();
    }

    public interface ILLMSessionLoader<T>
        where T : class, ILLMSession
    {
        static abstract Task<T?> LoadFromStream(Stream stream, IMapper mapper);
    }
}