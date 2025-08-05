using System.IO;

namespace LLMClient.Rag;

public class PdfFile : IRagFileSource
{
    public static PdfFile Load(string filePath)
    {
        if (string.IsNullOrEmpty(filePath.Trim()))
        {
            throw new ArgumentException($"'{nameof(filePath)}' cannot be null or empty.", nameof(filePath));
        }

        var fileInfo = new FileInfo(filePath);
        if (!fileInfo.Exists)
        {
            throw new FileNotFoundException(filePath);
        }

        return new PdfFile() { FilePath = filePath };
    }

    public string? FilePath { get; set; }

    public bool IsConstructed { get; set; } = false;

    public Task LoadAsync()
    {
        throw new NotImplementedException();
    }

    public Task ConstructAsync(CancellationToken cancellationToken = default)
    {
        if (IsConstructed)
        {
            return Task.CompletedTask;
        }

        throw new NotImplementedException();
    }

    public Task<ISearchResult> QueryAsync(string query, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}