using AutoMapper;

namespace LLMClient.Abstraction
{
    public interface ILoadableSessionFile<T>
        where T : class, ILLMSessionFile
    {
        static abstract Task<T?> LoadFromStream(Stream stream, IMapper mapper);
    }
}