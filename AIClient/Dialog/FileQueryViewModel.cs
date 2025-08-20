using System.Windows.Input;
using LLMClient.Abstraction;
using LLMClient.Rag;
using LLMClient.UI;
using LLMClient.UI.Component;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Xaml.Behaviors.Core;

namespace LLMClient.Dialog;

public class FileQueryViewModel : BaseViewModel
{
    private IRagSource? _selectedSource;
    public RequesterViewModel Requester { get; }
    public IRagSourceCollection RagSources => BaseViewModel.ServiceLocator.GetService<IRagSourceCollection>()!;

    public IRagSource? SelectedSource
    {
        get => _selectedSource;
        set
        {
            if (Equals(value, _selectedSource)) return;
            _selectedSource = value;
            OnPropertyChanged();
        }
    }

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

    public IList<SelectableViewModel<ChunkNode>>? SearchResults
    {
        get => _nodes;
        set
        {
            if (Equals(value, _nodes)) return;
            _nodes = value;
            OnPropertyChanged();
        }
    }

    private SearchAlgorithm _algorithm;
    private int? _topK = 6;
    private IList<SelectableViewModel<ChunkNode>>? _nodes;

    public ICommand SearchCommand => new ActionCommand(async _ =>
    {
        if (SelectedSource == null)
        {
            MessageEventBus.Publish("Please select a source to search.");
            return;
        }

        if (string.IsNullOrWhiteSpace(SearchText))
        {
            MessageEventBus.Publish("Search text cannot be empty.");
            return;
        }

        try
        {
            var searchResult = (StructResult)await SelectedSource.QueryAsync(_searchText, new
            {
                SearchAlgorithm = _algorithm,
                TopK = _topK
            });
            this.SearchResults = searchResult.Nodes.ToSelectable().ToArray();
            MessageEventBus.Publish("Search completed successfully.");
        }
        catch (Exception e)
        {
            MessageEventBus.Publish($"Search failed: {e.Message}");
        }
    });

    public FileQueryViewModel(RequesterViewModel requester)
    {
        Requester = requester;
    }
}