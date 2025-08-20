using System.Windows.Input;
using LLMClient.UI;
using LLMClient.UI.Component;
using Microsoft.Xaml.Behaviors.Core;

namespace LLMClient.Rag;

public class FileRagDataViewModel : BaseViewModel
{
    private string _searchText = string.Empty;

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (value == _searchText) return;
            _searchText = value;
            OnPropertyChanged();
        }
    }

    public SearchAlgorithm Algorithm
    {
        get => _algorithm;
        set
        {
            if (value == _algorithm) return;
            _algorithm = value;
            OnPropertyChanged();
        }
    }

    public int? TopK
    {
        get => _topK;
        set
        {
            if (value == _topK) return;
            _topK = value;
            OnPropertyChanged();
        }
    }

    public IList<ChunkNode>? SearchResults
    {
        get => _nodes;
        set
        {
            if (Equals(value, _nodes)) return;
            _nodes = value;
            OnPropertyChanged();
        }
    }

    public ICommand SearchCommand => new ActionCommand(async _ =>
    {
        if (string.IsNullOrWhiteSpace(SearchText))
        {
            MessageEventBus.Publish("Search text cannot be empty.");
            return;
        }

        try
        {
            var searchResult = (StructResult)await _ragFile.QueryAsync(_searchText, new
            {
                Algorithm = _algorithm,
                TopK = _topK
            });
            this.SearchResults = searchResult.Nodes;
            MessageEventBus.Publish("Search completed successfully.");
        }
        catch (Exception e)
        {
            MessageEventBus.Publish($"Search failed: {e.Message}");
        }
    });

    public ICommand ListStructureCommand => new ActionCommand(async _ =>
    {
        try
        {
            var structureResult = (StructResult)await _ragFile.GetStructureAsync();
            this.SearchResults = structureResult.Nodes;
            MessageEventBus.Publish("Structure retrieved successfully.");
        }
        catch (Exception e)
        {
            MessageEventBus.Publish($"Failed to retrieve structure: {e.Message}");
        }
    });

    public ICommand GetDocumentCommand => new ActionCommand(async _ =>
    {
        try
        {
            var result = (StructResult)await _ragFile.GetFullDocumentAsync();
            this.SearchResults = result.Nodes;
            MessageEventBus.Publish("Document retrieved successfully.");
        }
        catch (Exception e)
        {
            MessageEventBus.Publish($"Failed to retrieve document: {e.Message}");
        }
    });

    public ICommand GetSectionCommand => new ActionCommand(async o =>
    {
        if (o is not string titleName || string.IsNullOrWhiteSpace(titleName))
        {
            MessageEventBus.Publish("Title name cannot be empty.");
            return;
        }

        try
        {
            var sectionResult = (StructResult)await _ragFile.GetSectionAsync(titleName);
            this.SearchResults = sectionResult.Nodes;
            MessageEventBus.Publish("Section retrieved successfully.");
        }
        catch (Exception e)
        {
            MessageEventBus.Publish($"Failed to retrieve section: {e.Message}");
        }
    });

    private readonly RagFileBase _ragFile;
    private SearchAlgorithm _algorithm;
    private int? _topK = 6;
    private IList<ChunkNode>? _nodes;

    public FileRagDataViewModel(RagFileBase ragFile)
    {
        this._ragFile = ragFile;
    }
}