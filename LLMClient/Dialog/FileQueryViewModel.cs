using System.Text;
using System.Windows.Input;
using LLMClient.Abstraction;
using LLMClient.Data;
using LLMClient.Rag;
using LLMClient.UI.Component.Utility;
using LLMClient.UI.ViewModel;
using LLMClient.UI.ViewModel.Base;
using Microsoft.Xaml.Behaviors.Core;

namespace LLMClient.Dialog;

public class FileQueryViewModel : BaseViewModel
{
    private IRagSource? _selectedSource;
    public RequesterViewModel Requester { get; }

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

    public ICommand AppendToUserPromptCommand => new ActionCommand(_ =>
    {
        if (SearchResults == null) return;
        var selectedNodes = SearchResults.Where(n => n.IsSelected)
            .Select(model => model.Data).ToArray();
        if (!selectedNodes.Any())
        {
            return;
        }

        if (this.SelectedSource == null)
        {
            return;
        }

        var builder =
            new StringBuilder(
                $"The following sections are retrieved from the source of '{this.SelectedSource.ResourceName}', you can take use of them to answer the user query.\n\n");
        var view = selectedNodes.GetView();
        builder.Append(view);
        Requester.PromptString =
            Requester.PromptString == null ? builder.ToString() : Requester.PromptString + "\n" + builder;
        foreach (var selectedNode in selectedNodes)
        {
            RecursiveAddAttachment(selectedNode, Requester.Attachments, builder);
        }
    });

    void RecursiveAddAttachment(ChunkNode node, IList<Attachment> attachments, StringBuilder promptBuilder)
    {
        var chunk = node.Chunk;
        if (chunk.Type == (int)ChunkType.ContentUnit)
        {
            var imagesInBase64 = chunk.AttachmentImagesInBase64.ToArray();
            foreach (var base64String in imagesInBase64)
            {
                if (ImageExtensions.TryGetBinaryFromBase64(base64String, out var binary, out var extension))
                {
                    var attachment = Attachment.CreateFromBinaryImage(binary, $".{extension}");
                    attachments.Add(attachment);
                }
            }

            promptBuilder.AppendLine(
                $"Section {chunk.Title} has {imagesInBase64.Length} additional images, see attachment.");
        }
        else
        {
            foreach (var child in node.Children)
            {
                RecursiveAddAttachment(child, attachments, promptBuilder);
            }
        }
    }

    public FileQueryViewModel(RequesterViewModel requester)
    {
        Requester = requester;
    }
}