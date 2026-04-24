// #define TESTMODE

// #define DATACHANGE_TRACE

using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Text;
using AutoMapper;
using LLMClient.Abstraction;
using LLMClient.Component.ViewModel;
using LLMClient.Configuration;
using LLMClient.Dialog.Models;
using LLMClient.Endpoints;
using LLMClient.ToolCall;
using MaterialDesignThemes.Wpf;
using Microsoft.Agents.AI;

namespace LLMClient.Dialog;

public class DialogViewModel : DialogSessionViewModel, IPromptableSession
{
#if DATACHANGE_TRACE
    public bool Log { get; set; }

#endif

    //创建新实例后默认为changed
    private bool _isDataChanged = true;

    public override bool IsDataChanged
    {
        get { return _isDataChanged || Requester.IsDataChanged; }
        set
        {
            _isDataChanged = value;
            Requester.IsDataChanged = value;
#if DATACHANGE_TRACE
            if (value && Log)
            {
                //记录调用堆栈
                var stackTrace = new StackTrace();
                var frames = stackTrace.GetFrames();
                var stackInfo = new StringBuilder();
                foreach (var frame in frames)
                {
                    var method = frame.GetMethod();
                    if (method != null)
                    {
                        stackInfo.AppendLine($"{method.DeclaringType?.FullName}.{method.Name}");
                    }
                }

                Debug.WriteLine($"{this.Name}, Data changed at {DateTime.Now}, StackTrace: {stackInfo}");
            }
#endif
        }
    }

    private string? _userSystemPrompt;

    public string? UserSystemPrompt
    {
        get => _userSystemPrompt;
        set
        {
            if (value == _userSystemPrompt) return;
            _userSystemPrompt = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SystemPrompt));
        }
    }

    private ObservableCollection<PromptEntry> _extendedSystemPrompts = [];

    public ObservableCollection<PromptEntry> ExtendedSystemPrompts
    {
        get => _extendedSystemPrompts;
        set
        {
            if (Equals(value, _extendedSystemPrompts)) return;
            _extendedSystemPrompts.CollectionChanged -= ExtendedSystemPromptsOnCollectionChanged;
            _extendedSystemPrompts = value;
            value.CollectionChanged += ExtendedSystemPromptsOnCollectionChanged;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SystemPrompt));
        }
    }

    public override AIContextProvider[]? ContextProviders { get; } = null;

    public override IPromptCommandAggregate? PromptCommand { get; } = null;

    public override string? SystemPrompt
    {
        get
        {
            if (!ExtendedSystemPrompts.Any() && string.IsNullOrEmpty(UserSystemPrompt))
            {
                return null;
            }

            var stringBuilder = new StringBuilder();
            foreach (var promptEntry in ExtendedSystemPrompts)
            {
                stringBuilder.AppendLine(promptEntry.Prompt);
            }

            stringBuilder.AppendLine(UserSystemPrompt);
            return stringBuilder.ToString();
        }
    }


    private readonly string[] _notTrackingProperties =
    [
        nameof(ScrollViewItem),
        nameof(SearchText),
        nameof(CurrentParallelResponseViewItem),
        nameof(Shortcut)
    ];

    public RequesterViewModel Requester { get; }

    public DialogViewModel(string topic, string initialPrompt, ILLMChatClient modelClient,
        IMapper mapper, Summarizer summarizer, GlobalOptions options,
        IViewModelFactory factory, IDialogItem? rootNode = null, IDialogItem? currentLeaf = null)
        : base(options, summarizer, rootNode, currentLeaf)
    {
        this.Topic = topic;
        Requester = factory.CreateViewModel<RequesterViewModel>(initialPrompt, modelClient);
        Requester.ConnectedSession = this;
        var functionTreeSelector = Requester.FunctionTreeSelector;
        functionTreeSelector.AfterSelect += FunctionTreeSelectorOnAfterSelect;
        PropertyChanged += (_, e) =>
        {
            var propertyName = e.PropertyName;
            if (_notTrackingProperties.Contains(propertyName))
            {
                return;
            }

            IsDataChanged = true;
        };
        _extendedSystemPrompts.CollectionChanged += ExtendedSystemPromptsOnCollectionChanged;
    }

    private void ExtendedSystemPromptsOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        this.IsDataChanged = true;
        OnPropertyChanged(nameof(SystemPrompt));
    }

    public override void OnResponseCompleted(IResponse response)
    {
        base.OnResponseCompleted(response);
    }

    private void FunctionTreeSelectorOnAfterSelect()
    {
        this.SelectedFunctionGroups =
            this.Requester.FunctionTreeSelector.FunctionGroups.Where(tree => tree.IsSelected != false).ToArray();
        PopupBox.ClosePopupCommand.Execute(null, null);
    }
}