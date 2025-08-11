using System.IO;
using System.Text.Json.Serialization;
using System.Windows.Input;
using LLMClient.UI;
using Microsoft.Xaml.Behaviors.Core;

namespace LLMClient.Rag;

public abstract class RagFileBase : BaseViewModel, IRagFileSource
{
    private string _name = string.Empty;
    private string? _errorMessage;
    private ConstructStatus _status = ConstructStatus.NotConstructed;

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

    public ConstructStatus Status
    {
        get => _status;
        set
        {
            if (value == _status) return;
            _status = value;
            OnPropertyChanged();
        }
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        set
        {
            if (value == _errorMessage) return;
            _errorMessage = value;
            OnPropertyChanged();
        }
    }

    [JsonIgnore] public abstract DocumentFileType FileType { get; }

    [JsonIgnore]
    public virtual string DocumentId
    {
        get { return $"{FileType}_{Id}"; }
    }

    public abstract Task LoadAsync();

    private CancellationTokenSource? _constructionCancellationTokenSource;
    private Task? _constructionTask;

    public ICommand StartConstructCommand => new ActionCommand(async o =>
    {
        if (_constructionTask is { IsCompleted: false })
        {
            // If already constructing, cancel the previous operation and wait for it to complete.
            await StopConstruct();
        }

        _constructionCancellationTokenSource = new CancellationTokenSource();
        _constructionTask = ConstructAsync(_constructionCancellationTokenSource.Token);
        try
        {
            await _constructionTask;
        }
        finally
        {
            _constructionCancellationTokenSource?.Dispose();
            _constructionCancellationTokenSource = null;
        }
    });

    public ICommand StopConstructCommand => new ActionCommand(async o => { await StopConstruct(); });

    public virtual async Task ConstructAsync(CancellationToken cancellationToken = default)
    {
        if (Status == ConstructStatus.Constructing ||
            Status == ConstructStatus.Constructed)
        {
            // Already constructing or constructed or error, no need to construct again.
            return;
        }

        Status = ConstructStatus.Constructing;
        try
        {
            ErrorMessage = null;
            await ConstructCore(cancellationToken);
            Status = ConstructStatus.Constructed;
        }
        catch (Exception e)
        {
            ErrorMessage = e.Message;
            Status = ConstructStatus.Error;
        }
    }

    public async Task StopConstruct()
    {
        if (_constructionCancellationTokenSource != null && _constructionTask != null)
        {
            await _constructionCancellationTokenSource.CancelAsync();
            try
            {
                await _constructionTask;
            }
            catch (OperationCanceledException)
            {
                // Expected, suppress to prevent unhandled exception.
            }
        }
    }

    public abstract Task DeleteAsync(CancellationToken cancellationToken = default);

    protected abstract Task ConstructCore(CancellationToken cancellationToken = default);

    public abstract Task<ISearchResult> QueryAsync(string query, dynamic options,
        CancellationToken cancellationToken = default);
}

public class SimpleQueryResult : ISearchResult
{
    public string? DocumentId { get; set; }

    public IList<string>? TextBlocks { get; set; }

    public SimpleQueryResult(string documentId, IList<string>? textBlocks)
    {
        DocumentId = documentId;
        TextBlocks = textBlocks;
    }
}