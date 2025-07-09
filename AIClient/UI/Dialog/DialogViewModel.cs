// #define TESTMODE

using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using LLMClient.Abstraction;
using LLMClient.Data;
using LLMClient.Endpoints;
using LLMClient.UI.Component;
using LLMClient.UI.MCP.Servers;
using MaterialDesignThemes.Wpf;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using Microsoft.Xaml.Behaviors.Core;

namespace LLMClient.UI.Dialog;

public class DialogViewModel : DialogCoreViewModel
{
    /// <summary>
    /// indicate whether data is changed after loading.
    /// </summary>
    public bool IsDataChanged { get; set; } = true;

    private string _topic;

    public string Topic
    {
        get => _topic;
        set
        {
            if (value == _topic) return;
            _topic = value;
            OnPropertyChanged();
        }
    }

    public string? Shortcut
    {
        get
        {
            return DialogItems.OfType<MultiResponseViewItem>()
                .FirstOrDefault(item => { return item.IsAvailableInContext; })
                ?.CurrentResponse?.TextWithoutThinking;
        }
    }

    #region request chain

    public bool IsChaining
    {
        get => _isChaining;
        set
        {
            if (value == _isChaining) return;
            _isChaining = value;
            OnPropertyChanged();
        }
    }

    public int ChainStepCount
    {
        get => _chainStepCount;
        set
        {
            if (value == _chainStepCount) return;
            _chainStepCount = value;
            OnPropertyChanged();
        }
    }

    public int ChainingStep
    {
        get => _chainingStep;
        set
        {
            if (value == _chainingStep) return;
            _chainingStep = value;
            OnPropertyChanged();
        }
    }

    public async void SequentialChain(IEnumerable<IDialogItem> dialogItems)
    {
        var client = this.DefaultClient;
        this.RespondingCount++;
        this.IsChaining = true;
        this.ChainingStep = 0;
        var pendingItems = dialogItems
            .Where(item => item is RequestViewItem || item is EraseViewItem)
            .ToArray();
        this.ChainStepCount = pendingItems.Length;
        try
        {
            foreach (var oldDialogDialogItem in pendingItems)
            {
                if (oldDialogDialogItem is RequestViewItem requestViewItem)
                {
                    var newGuid = Guid.NewGuid();
                    var newItem = requestViewItem.Clone();
                    DialogItems.Add(newItem);
                    var copy = DialogItems.ToArray();
                    int retryCount = 3;
                    while (retryCount > 0)
                    {
                        var multiResponseViewItem = new MultiResponseViewItem() { InteractionId = newGuid };
                        DialogItems.Add(multiResponseViewItem);
                        var completedResult =
                            await SendRequestCore(client, copy, multiResponseViewItem);
                        if (!completedResult.IsInterrupt)
                        {
                            break;
                        }

                        DialogItems.Remove(multiResponseViewItem);
                        retryCount--;
                    }

                    if (retryCount == 0)
                    {
                        MessageBox.Show("请求失败，重试次数已用完！", "错误", MessageBoxButton.OK,
                            MessageBoxImage.Error);
                    }
                }
                else if (oldDialogDialogItem is EraseViewItem)
                {
                    this.DialogItems.Add(new EraseViewItem());
                }

                ChainingStep++;
            }
        }
        catch (Exception exception)
        {
            MessageBox.Show("重试处理对话失败: " + exception.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            this.RespondingCount--;
            this.IsChaining = false;
            /*this.ChainStepCount = 0;
            this.ChainingStep = 0;*/
            ScrollViewItem = DialogItems.Last();
        }
    }

    #endregion

    #region search

    private string? _searchText;

    public string? SearchText
    {
        get => _searchText;
        set
        {
            if (value == _searchText) return;
            _searchText = value;
            OnPropertyChanged();
        }
    }

    public ICommand SearchCommand => new ActionCommand((o =>
    {
        foreach (var dialogViewItem in this.DialogItems)
        {
            if (dialogViewItem is MultiResponseViewItem multiResponseViewItem)
            {
                foreach (var responseViewItem in multiResponseViewItem.Items.OfType<ResponseViewItem>())
                {
                    responseViewItem.Document?.ApplySearch(_searchText);
                }
            }
        }

        this.FocusedResponse = null;
        if (this.ScrollViewItem is MultiResponseViewItem viewItem)
        {
            viewItem.CurrentResponse?.Document?.EnsureSearch();
        }
    }));

    private int _currentHighlightIndex = 0;

    private MultiResponseViewItem? FocusedResponse
    {
        get => _focusedResponse;
        set
        {
            _focusedResponse = value;
            var document = value?.CurrentResponse?.Document;
            if (document is { HasMatched: true })
            {
                document.EnsureSearch();
            }
        }
    }

    SearchableDocument? FocusedDocument
    {
        get { return FocusedResponse?.CurrentResponse?.Document; }
    }

    private void CheckFocusResponse(IList<MultiResponseViewItem> responseViewItems, ref int responseIndex)
    {
        if (FocusedResponse != null)
        {
            responseIndex = responseViewItems.IndexOf(FocusedResponse);
            if (responseIndex == -1)
            {
                FocusedResponse = null;
                responseIndex = 0;
            }
        }

        if (FocusedResponse == null)
        {
            this.FocusedResponse = responseViewItems.First();
        }
    }

    private void GoToHighlight()
    {
        ScrollViewItem = this.FocusedResponse;
        var foundTextRange = FocusedDocument?.FoundTextRanges[_currentHighlightIndex];
        if (foundTextRange == null)
            return;
        var parent = FocusedDocument?.Document.Parent;
        if (parent is FlowDocumentScrollViewerEx ex)
        {
            ex.ScrollToRange(foundTextRange);
        }
    }

    public ICommand GoToNextHighlightCommand => new ActionCommand((o =>
    {
        if (string.IsNullOrEmpty(SearchText))
        {
            return;
        }

        var responseViewItems = DialogItems.OfType<MultiResponseViewItem>()
            .Where(item => item.AcceptedResponse is ResponseViewItem { Document.HasMatched: true }).ToArray();
        if (responseViewItems.Length == 0)
        {
            MessageEventBus.Publish("没有找到匹配的结果！");
            return;
        }

        var responseIndex = 0;
        CheckFocusResponse(responseViewItems, ref responseIndex);
        _currentHighlightIndex++;
        if (_currentHighlightIndex >= FocusedDocument?.FoundTextRanges.Count)
        {
            //跳转到下一个FlowDocument
            responseIndex++;
            FocusedResponse = responseIndex < responseViewItems.Length
                ? responseViewItems[responseIndex]
                : responseViewItems[0];
            _currentHighlightIndex = 0;
        }

        GoToHighlight();
    }));

    public ICommand GoToPreviousHighlightCommand => new ActionCommand((o =>
    {
        if (string.IsNullOrEmpty(SearchText))
        {
            return;
        }

        var responseViewItems = DialogItems.OfType<MultiResponseViewItem>()
            .Where(item => item.AcceptedResponse is ResponseViewItem { Document.HasMatched: true }).ToArray();
        if (responseViewItems.Length == 0)
        {
            MessageEventBus.Publish("没有找到匹配的结果！");
            return;
        }

        var responseIndex = responseViewItems.Length - 1;
        CheckFocusResponse(responseViewItems, ref responseIndex);
        _currentHighlightIndex--;
        if (_currentHighlightIndex < 0)
        {
            //跳转到上一个FlowDocument
            FocusedResponse = responseIndex > 0
                ? responseViewItems[--responseIndex]
                : responseViewItems.Last();
            _currentHighlightIndex = FocusedDocument?.FoundTextRanges.Count - 1 ?? 0;
        }

        GoToHighlight();
    }));

    #endregion

    #region toolbar

    public ICommand ClearUnavailableCommand => new ActionCommand((o =>
    {
        var deleteItems = new List<IDialogItem>();
        Guid unusedInteractionId = Guid.Empty;
        for (var index = DialogItems.Count - 1; index >= 0; index--)
        {
            var dialogViewItem = DialogItems[index];
            if (dialogViewItem is MultiResponseViewItem item && !item.HasAvailableMessage)
            {
                deleteItems.Add(dialogViewItem);
                unusedInteractionId = item.InteractionId;
            }
            else if (dialogViewItem is RequestViewItem requestViewItem &&
                     requestViewItem.InteractionId == unusedInteractionId)
            {
                deleteItems.Add(requestViewItem);
            }
        }

        deleteItems.ForEach(item => DialogItems.Remove(item));
    }));

    public ICommand ChangeModelCommand => new ActionCommand((async o =>
    {
        var selectionViewModel = new DialogCreationViewModel(EndpointService);
        if (await DialogHost.Show(selectionViewModel) is true)
        {
            var model = selectionViewModel.GetClient();
            if (model == null)
            {
                MessageBox.Show("No model created!");
                return;
            }

            this.DefaultClient = model;
        }
    }));

    public ICommand ExportCommand => new ActionCommand((async o =>
    {
        var saveFileDialog = new SaveFileDialog()
        {
            AddExtension = true,
            DefaultExt = ".md", CheckPathExists = true,
            Filter = "markdown files (*.md)|*.md"
        };
        if (saveFileDialog.ShowDialog() != true)
        {
            return;
        }

        var stringBuilder = new StringBuilder(8192);
        stringBuilder.AppendLine($"# {this.Topic}");
        stringBuilder.AppendLine($"### {this.DefaultClient.Name}");
        foreach (var viewItem in this.DialogItems.Where((item => item.IsAvailableInContext)))
        {
            if (viewItem is MultiResponseViewItem multiResponseView &&
                multiResponseView.AcceptedResponse is ResponseViewItem responseViewItem)
            {
                var textContent = responseViewItem.TextContent;
                stringBuilder.AppendLine("## **Assistant:**");
                stringBuilder.Append(textContent ?? string.Empty);
                stringBuilder.AppendLine();
                stringBuilder.AppendLine();
                stringBuilder.AppendLine("***");
                stringBuilder.AppendLine();
            }
            else if (viewItem is RequestViewItem reqViewItem)
            {
                stringBuilder.AppendLine("## **User:**");
                stringBuilder.Append(reqViewItem.TextMessage);
                stringBuilder.AppendLine();
                stringBuilder.AppendLine();
                stringBuilder.AppendLine("***");
                stringBuilder.AppendLine();
            }
        }

        var fileName = saveFileDialog.FileName;
        await File.WriteAllTextAsync(fileName, stringBuilder.ToString());
        MessageEventBus.Publish("已导出");
    }));

    #endregion

    /*public ICommand TestCommand => new ActionCommand((async o =>
    {
        if (this.Client == null)
        {
            return;
        }

        var dialogViewItem = this.DialogItems.Last(item => item is MultiResponseViewItem && item.IsAvailableInContext);
        var multiResponseViewItem = dialogViewItem as MultiResponseViewItem;
        var endpoint = EndpointService.AvailableEndpoints[0];
        var first = endpoint.AvailableModelNames.First();
        var llmModelClient = new ModelSelectionViewModel(this.EndpointService)
        {
            SelectedModelName = first,
            SelectedEndpoint = endpoint
        }.GetClient();
        if (llmModelClient == null)
        {
            return;
        }

        if (multiResponseViewItem != null)
        {
            await AppendResponseOn(multiResponseViewItem, llmModelClient);
        }
    }));*/

    public IPromptsResource PromptsResource
    {
        get { return ServiceLocator.GetService<IPromptsResource>()!; }
    }

    private ILLMClient _defaultClient;

    public override ILLMClient DefaultClient
    {
        get => _defaultClient;
        set
        {
            if (Equals(value, _defaultClient)) return;
            if (_defaultClient is INotifyPropertyChanged oldValue)
            {
                oldValue.PropertyChanged -= TagDataChangedOnPropertyChanged;
            }

            if (_defaultClient.Parameters is INotifyPropertyChanged oldParameters)
            {
                oldParameters.PropertyChanged -= TagDataChangedOnPropertyChanged;
            }

            _defaultClient = value;
            OnPropertyChanged();
            TrackClientChanged(value);
        }
    }
    
    private MultiResponseViewItem? _focusedResponse;
    private bool _isChaining;
    private int _chainStepCount;
    private int _chainingStep;

    private readonly string[] _notTrackingProperties =
    [
        nameof(DialogViewModel.ScrollViewItem),
        nameof(DialogViewModel.SearchText)
    ];

    public DialogViewModel(string topic, ILLMClient modelClient,
        IList<IDialogItem>? items = null) : base(items)
    {
        this._topic = topic;
        this._defaultClient = modelClient;
        this.TrackClientChanged(modelClient);
        this.DialogItems.CollectionChanged += DialogOnCollectionChanged;
        this.PropertyChanged += (_, e) =>
        {
            var propertyName = e.PropertyName;
            if (this._notTrackingProperties.Contains(propertyName))
            {
                return;
            }

            this.IsDataChanged = true;
        };
    }

    private void TagDataChangedOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        IsDataChanged = true;
    }

    private void TrackClientChanged(ILLMClient client)
    {
        if (client is INotifyPropertyChanged newValue)
        {
            newValue.PropertyChanged += TagDataChangedOnPropertyChanged;
        }

        if (_defaultClient.Parameters is INotifyPropertyChanged newParameters)
        {
            newParameters.PropertyChanged += TagDataChangedOnPropertyChanged;
        }
    }

    private void DialogOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        this.IsDataChanged = true;
        OnPropertyChangedAsync(nameof(Shortcut));
        OnPropertyChanged(nameof(CurrentContextTokens));
    }
}