using System.IO;
using System.Text.Json.Serialization;
using LLMClient.UI;

namespace LLMClient.Rag;

public abstract class RagFileBase : BaseViewModel, IRagFileSource
{
    private string _name = string.Empty;
    private bool _hasConstructed;

    public string Name
    {
        get => _name;
        set
        {
            if (value == _name) return;
            _name = value;
            OnPropertyChanged();
        }
    }

    public Guid Id { get; set; } = Guid.NewGuid();

    protected RagFileBase()
    {
    }

    protected RagFileBase(FileInfo fileInfo)
    {
        this.FilePath = fileInfo.FullName;
        this.FileSize = fileInfo.Length;
        this.EditTime = fileInfo.LastWriteTime;
        this.Name = fileInfo.Name;
    }

    public string FilePath { get; set; } = string.Empty;
    public DateTime EditTime { get; set; }
    public long FileSize { get; set; } = 0;

    public bool HasConstructed
    {
        get => _hasConstructed;
        set
        {
            if (value == _hasConstructed) return;
            _hasConstructed = value;
            OnPropertyChanged();
        }
    }

    [JsonIgnore] public abstract DocumentFileType FileType { get; }

    public abstract Task LoadAsync();

    public virtual Task ConstructAsync(CancellationToken cancellationToken = default)
    {
        return HasConstructed ? Task.CompletedTask : ConstructCore(cancellationToken);
    }

    public abstract Task DeleteAsync(CancellationToken cancellationToken = default);

    protected abstract Task ConstructCore(CancellationToken cancellationToken = default);

    public abstract Task<ISearchResult> QueryAsync(string query, CancellationToken cancellationToken = default);
}