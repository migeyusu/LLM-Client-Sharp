using System.Reflection.Metadata;
using System.Text.Json.Serialization;
using LLMClient.Abstraction;

namespace LLMClient.Rag;

public enum DocumentFileType
{
    Text,
    Word,
    Pdf,
    Excel,
    Markdown,
}

public interface IRagFileSource : IRagSource
{
    string FilePath { get; }

    DocumentFileType FileType { get; }

    DateTime EditTime { get; }

    long FileSize { get; }

    string DocumentId { get; }
}

public enum RagFileStatus
{
    NotConstructed,
    Constructing,
    Constructed,
    Error
}