using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using LLMClient.Abstraction;
using LLMClient.Component.Render;
using LLMClient.Component.UserControls;
using LLMClient.Component.Utility;
using LLMClient.Component.ViewModel;
using LLMClient.Component.ViewModel.Base;
using LLMClient.Configuration;
using LLMClient.Dialog.Models;
using LLMClient.Endpoints;
using LLMClient.ToolCall;
using MaterialDesignThemes.Wpf;
using Microsoft.Extensions.AI;
using Microsoft.Xaml.Behaviors.Core;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using TextBoxBase = System.Windows.Controls.Primitives.TextBoxBase;

namespace LLMClient.Dialog;

public class RequesterViewModel : BaseViewModel
{
    /// <summary>
    /// indicate whether data is changed after loading.
    /// </summary>
    public bool IsDataChanged { get; set; } = true;

    public event Action<IResponse>? RequestCompleted;

    public ICommand NewRequestCommand => new ActionCommand(async o =>
    {
        try
        {
            var requestViewItem = await this.NewRequest();
            if (requestViewItem == null)
            {
                return;
            }

            IsNewResponding = true;
            using (_tokenSource = new CancellationTokenSource())
            {
                var completedResult =
                    await _getResponse.Invoke(this.DefaultClient, requestViewItem, null, _tokenSource.Token);
                OnRequestCompleted(completedResult);
                if (!completedResult.IsInterrupt)
                {
                    ClearRequest();
                }
            }
        }
        catch (Exception e)
        {
            MessageEventBus.Publish(e.Message);
        }
        finally
        {
            IsNewResponding = false;
        }
    });

    public bool RawEditMode
    {
        get => _rawEditMode;
        set
        {
            if (value == _rawEditMode) return;
            _rawEditMode = value;
            OnPropertyChanged();
            RefreshEditor(value);
        }
    }

    public ICommand SummarizeCommand => new ActionCommand(_ => { Summarize(); });

    public ICommand ChangeModelCommand => new ActionCommand(async o =>
    {
        var selectionViewModel = new ModelSelectionPopupViewModel((model =>
        {
            this.DefaultClient = model.CreateClient();
        }));
        await DialogHost.Show(selectionViewModel);
    });

    private ILLMChatClient _defaultClient;

    public ILLMChatClient DefaultClient
    {
        get => _defaultClient;
        set
        {
            if (Equals(value, _defaultClient)) return;
            if (_defaultClient is INotifyPropertyChanged oldValue)
            {
                oldValue.PropertyChanged -= TagDataChanged;
            }

            if (_defaultClient.Parameters is INotifyPropertyChanged oldParameters)
            {
                oldParameters.PropertyChanged -= TagDataChanged;
            }

            _defaultClient = value;
            BindClient(value);
            OnPropertyChanged();
        }
    }

    #region rag

    private readonly IRagSourceCollection _ragSourceCollection;

    //rag有两种利用模式：search和plugin模式，search模式由手动调用，可产生结果并入上下文；plugin由llm驱动调用

    private IList<SelectableViewModel<IRagSource>> _ragSources = Array.Empty<SelectableViewModel<IRagSource>>();

    //不需要持久化
    public IList<SelectableViewModel<IRagSource>> RagSources
    {
        get => _ragSources;
        set
        {
            if (Equals(value, _ragSources)) return;
            _ragSources = value;
            OnPropertyChanged();
        }
    }

    public void NotifyRagSelection()
    {
        OnPropertyChanged(nameof(IsRagEnabled));
    }

    public bool IsRagEnabled => RagSources.Any(model => model.IsSelected && model.Data.IsAvailable);

    public void RefreshRagSources()
    {
        var selected = RagSources.Where(model => model.IsSelected).ToList();
        var selectableViewModels = _ragSourceCollection.Sources.ToSelectable().ToArray();
        foreach (var selectableViewModel in selectableViewModels)
        {
            var dataId = selectableViewModel.Data.Id;
            if (selected.Find((model => model.Data.Id == dataId)) != null)
            {
                selectableViewModel.IsSelected = true;
            }
        }

        RagSources = selectableViewModels;
    }

    public FileQueryViewModel QueryViewModel { get; }

    #endregion

    #region attachment

    /// <summary>
    /// 这里的Attachment不会持久化，只有发出之后的请求中附带的attchment会被持久化（缓存）。
    /// </summary>
    public ObservableCollection<Attachment> Attachments { get; set; } =
        new();

    public ICommand AddCodeFileCommand { get; }

    public ICommand AddImageCommand { get; }

    public ICommand RemoveAttachmentCommand { get; }

    #endregion

    #region function call

    public AIFunctionTreeSelectorViewModel FunctionTreeSelector { get; }

    #endregion

    public SearchConfigViewModel SearchConfig { get; }

    #region input

    public TextContentEditViewModel PromptEditViewModel
    {
        get => _promptEditViewModel;
        private set
        {
            if (Equals(value, _promptEditViewModel)) return;
            _promptEditViewModel = value;
            OnPropertyChanged();
        }
    }

    public long EstimatedTokens
    {
        get
        {
            return GetAsyncProperty(async () =>
            {
                var contentText = PromptEditViewModel.FinalText;
                if (string.IsNullOrEmpty(contentText))
                {
                    return 0;
                }

                return await _tokensCounter.CountTokens(contentText);
            });
        }
    }

    public ICommand CancelLastCommand { get; }

    private bool _isDebugMode = true;

    public bool IsDebugMode
    {
        get => _isDebugMode;
        set
        {
            if (value == _isDebugMode) return;
            _isDebugMode = value;
            OnPropertyChanged();
        }
    }

    private bool _isNewResponding;

    public bool IsNewResponding
    {
        get => _isNewResponding;
        private set
        {
            if (value == _isNewResponding) return;
            _isNewResponding = value;
            OnPropertyChanged();
        }
    }

    public IFunctionGroupSource? FunctionGroupSource { get; set; }

    #endregion

    private readonly Func<ILLMChatClient, IRequestItem, IRequestItem?, CancellationToken, Task<ChatCallResult>>
        _getResponse;

    private readonly GlobalOptions _options;
    private readonly Summarizer _summarizer;

    private readonly ITokensCounter _tokensCounter;

    public RequesterViewModel(string initialPrompt, ILLMChatClient modelClient,
        Func<ILLMChatClient, IRequestItem, IRequestItem?, CancellationToken, Task<ChatCallResult>> getResponse,
        GlobalOptions options, Summarizer summarizer, IRagSourceCollection ragSourceCollection,
        ITokensCounter tokensCounter)
    {
        FunctionTreeSelector = new AIFunctionTreeSelectorViewModel();
        SearchConfig = new SearchConfigViewModel();
        _promptEditViewModel = new TextContentCodeEditViewModel(new TextContent(initialPrompt), null);
        QueryViewModel = new FileQueryViewModel(this);
        this._defaultClient = modelClient;
        this._getResponse = getResponse;
        _options = options;
        _summarizer = summarizer;
        _ragSourceCollection = ragSourceCollection;
        _tokensCounter = tokensCounter;
        this.BindClient(modelClient);
        CancelLastCommand = new ActionCommand(_ => { _tokenSource?.Cancel(); });
        AddCodeFileCommand = new RelayCommand<object>((o =>
        {
            var textBoxBase = ((UIElement?)o)?.FindVisualChild<TextBoxBase>();
            if (textBoxBase == null)
            {
                return;
            }

            //可以添加代码文件，并已markdown格式插入到输入框中
            var openFileDialog = new OpenFileDialog()
            {
                Filter = "Text files (*.*)|*.*",
                Multiselect = true
            };
            if (openFileDialog.ShowDialog() != true)
            {
                return;
            }

            var settingsOptions = TextMateCodeRenderer.Settings.Options;

            if (textBoxBase is RichTextBox richTextBox)
            {
                foreach (var fileName in openFileDialog.FileNames)
                {
                    var content = File.ReadAllText(fileName);
                    var extension = Path.GetExtension(fileName);
                    var language = settingsOptions.GetLanguageByExtension(extension)?.Id ?? extension.TrimStart('.');

                    var codeVm = new EditableCodeViewModel(content, extension, language)
                    {
                        FileLocation = fileName
                    };
                    
                    var expander = new Expander
                    {
                        Content = codeVm,
                        IsExpanded = true,
                        Header = codeVm
                    };

                    expander.SetResourceReference(FrameworkElement.StyleProperty, TextMateCodeRenderer.EditCodeBlockStyleKey);

                    var block = new BlockUIContainer(expander);

                    if (richTextBox.CaretPosition.Paragraph != null)
                    {
                        richTextBox.CaretPosition = richTextBox.CaretPosition.InsertParagraphBreak();
                    }

                    var paragraph = richTextBox.CaretPosition.Paragraph;
                    if (paragraph != null)
                    {
                        richTextBox.Document.Blocks.InsertBefore(paragraph, block);
                    }
                    else
                    {
                        richTextBox.Document.Blocks.Add(block);
                    }

                    richTextBox.CaretPosition = block.ElementEnd.GetInsertionPosition(LogicalDirection.Forward);
                }
                
                richTextBox.Focus();
            }
            else
            {
                var stringBuilder = new StringBuilder();
                stringBuilder.AppendLine();
                foreach (var fileName in openFileDialog.FileNames)
                {
                    var extension = Path.GetExtension(fileName);
                    var language = settingsOptions.GetLanguageByExtension(extension)?.Id ?? extension;
                    stringBuilder.Append("```")
                        .Append(language)
                        .Append($" file=\"{fileName}\"");
                    stringBuilder.AppendLine();
                    stringBuilder.AppendLine(File.ReadAllText(fileName));
                    stringBuilder.AppendLine("```");
                }

                textBoxBase.Focus();
                if (textBoxBase is TextBox textBox)
                {
                    textBox.SelectedText = stringBuilder.ToString();
                }
            }

            this.IsDataChanged = true;
        }));
        AddImageCommand = new ActionCommand(_ =>
        {
            var openFileDialog = new OpenFileDialog()
            {
                Filter = "Image files (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg",
                Multiselect = true
            };
            if (openFileDialog.ShowDialog() != true)
            {
                return;
            }

            foreach (var fileName in openFileDialog.FileNames)
            {
                this.Attachments.Add(Attachment.CreateFromLocal(fileName, AttachmentType.Image));
            }
        });
        RemoveAttachmentCommand = new ActionCommand(o =>
        {
            if (o is Attachment attachment)
            {
                this.Attachments.Remove(attachment);
            }
        });
        this.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(EstimatedTokens))
            {
                return;
            }

            this.IsDataChanged = true;
        };
    }

    private void BindClient(ILLMChatClient client)
    {
        if (client is INotifyPropertyChanged newValue)
        {
            newValue.PropertyChanged += TagDataChanged;
        }

        if (_defaultClient.Parameters is INotifyPropertyChanged newParameters)
        {
            newParameters.PropertyChanged += TagDataChanged;
        }

        this.SearchConfig.ResetSearch(client);
        this.FunctionTreeSelector.SelectableCallEngineTypes = DefaultClient.Model.SupportFunctionCall
            ?
            [
                FunctionCallEngineType.OpenAI,
                FunctionCallEngineType.Prompt
            ]
            :
            [
                FunctionCallEngineType.Prompt
            ];
    }

    private async void RefreshEditor(bool rawMode)
    {
        if (rawMode && PromptEditViewModel is TextContentCodeEditViewModel codeVm)
        {
            await codeVm.ApplyText();
            PromptEditViewModel = new TextContentRawEditViewModel(new TextContent(codeVm.FinalText),
                codeVm.MessageId);
        }
        else if (!rawMode && PromptEditViewModel is TextContentRawEditViewModel rawVm)
        {
            await rawVm.ApplyText();
            PromptEditViewModel = new TextContentCodeEditViewModel(new TextContent(rawVm.FinalText),
                rawVm.MessageId);
        }
    }

    public void ClearRequest()
    {
        this.Attachments.Clear();
        var textContent = new TextContent(string.Empty);
        this.PromptEditViewModel = this.RawEditMode
            ? new TextContentRawEditViewModel(textContent, null)
            : new TextContentCodeEditViewModel(textContent, null);
        InvalidateAsyncProperty(nameof(EstimatedTokens));
    }


    private CancellationTokenSource? _tokenSource;
    private TextContentEditViewModel _promptEditViewModel;
    private bool _rawEditMode;

    public async void Summarize(IRequestItem? insertBefore = null)
    {
        IsNewResponding = true;
        var summaryRequest = _summarizer.CreateRequest();
        try
        {
            var summarizeModel = _options.CreateContextSummarizeClient() ?? this.DefaultClient;
            using (_tokenSource = new CancellationTokenSource())
            {
                await _getResponse.Invoke(summarizeModel, summaryRequest, insertBefore, _tokenSource.Token);
            }
        }
        catch (Exception e)
        {
            MessageEventBus.Publish(e.Message);
        }
        finally
        {
            summaryRequest.IsSummarizing = false;
            IsNewResponding = false;
        }
    }

    public async Task<RequestViewItem?> NewRequest(string additionalPrompt = "")
    {
        if (!await PromptEditViewModel.ApplyAndCheck())
        {
            return null;
        }

        var promptBuilder = new StringBuilder();
        if (!string.IsNullOrEmpty(additionalPrompt))
        {
            promptBuilder.Append(additionalPrompt);
        }

        promptBuilder.Append(PromptEditViewModel.FinalText);
        IList<CheckableFunctionGroupTree>? tools = null;
        if (this.FunctionTreeSelector.FunctionSelected)
        {
            tools = this.FunctionGroupSource?.GetFunctionGroups().OfType<CheckableFunctionGroupTree>().ToArray();
        }

        var ragSources = RagSources.Where(model => model is { IsSelected: true, Data.IsAvailable: true })
            .Select(model => model.Data)
            .ToArray();
        //每次搜索的条件可能不同，所以传递的是副本
        return new RequestViewItem(promptBuilder.ToString())
        {
            Attachments = Attachments.Count == 0 ? null : Attachments.ToList(),
            FunctionGroups = tools == null ? null : [..tools],
            SearchOption = SearchConfig.GetUserSearchOption(),
            RagSources = ragSources.Length > 0 ? ragSources : null,
            CallEngine = this.FunctionTreeSelector.EngineType ??
                         this.FunctionTreeSelector.SelectableCallEngineTypes.FirstOrDefault(),
            IsDebugMode = this.IsDebugMode
        };
    }

    private void TagDataChanged(object? sender, PropertyChangedEventArgs e)
    {
        IsDataChanged = true;
    }

    protected virtual void OnRequestCompleted(IResponse obj)
    {
        RequestCompleted?.Invoke(obj);
    }
}