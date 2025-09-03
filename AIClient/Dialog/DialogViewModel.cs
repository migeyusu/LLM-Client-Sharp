// #define TESTMODE

using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Input;
using AutoMapper;
using LLMClient.Abstraction;
using LLMClient.Data;
using LLMClient.MCP;
using LLMClient.MCP.Servers;
using LLMClient.UI;
using LLMClient.UI.Component;
using MaterialDesignThemes.Wpf;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Xaml.Behaviors.Core;
using MessageBox = System.Windows.MessageBox;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;

namespace LLMClient.Dialog;

public class DialogViewModel : DialogSessionViewModel, IFunctionGroupSource
{
    //创建新实例后默认为changed
    private bool _isDataChanged = true;

    public override bool IsDataChanged
    {
        get { return _isDataChanged | Requester.IsDataChanged; }
        set
        {
            _isDataChanged = value;
            Requester.IsDataChanged = value;
        }
    }

    private string? _systemPrompt;

    public override string? SystemPrompt
    {
        get => _systemPrompt;
        set
        {
            if (value == _systemPrompt) return;
            _systemPrompt = value;
            OnPropertyChanged();
        }
    }

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
        var client = Requester.DefaultClient;
        RespondingCount++;
        IsChaining = true;
        ChainingStep = 0;
        var pendingItems = dialogItems
            .Where(item => item is IRequestItem || item is EraseViewItem)
            .ToArray();
        ChainStepCount = pendingItems.Length;
        try
        {
            foreach (var oldDialogDialogItem in pendingItems)
            {
                if (oldDialogDialogItem is IRequestItem requestViewItem)
                {
                    var newGuid = Guid.NewGuid();
                    var newItem = Extension.Clone(requestViewItem);
                    DialogItems.Add(newItem);
                    var copy = GenerateHistoryFromSelf();
                    int retryCount = 3;
                    while (retryCount > 0)
                    {
                        var multiResponseViewItem = new MultiResponseViewItem(this) { InteractionId = newGuid };
                        DialogItems.Add(multiResponseViewItem);
                        var completedResult =
                            await SendRequestCore(client, copy, multiResponseViewItem, this.SystemPrompt);
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
                    DialogItems.Add(new EraseViewItem());
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
            RespondingCount--;
            IsChaining = false;
            /*this.ChainStepCount = 0;
            this.ChainingStep = 0;*/
            ScrollViewItem = DialogItems.Last();
        }
    }

    #endregion

    private bool _isChaining;
    private int _chainStepCount;
    private int _chainingStep;

    private readonly string[] _notTrackingProperties =
    [
        nameof(ScrollViewItem),
        nameof(SearchText)
    ];

    private IList<CheckableFunctionGroupTree>? _selectedFunctionGroups;

    public RequesterViewModel Requester { get; }

    public IList<CheckableFunctionGroupTree>? SelectedFunctionGroups
    {
        get => _selectedFunctionGroups;
        set
        {
            if (Equals(value, _selectedFunctionGroups)) return;
            _selectedFunctionGroups = value;
            OnPropertyChanged();
        }
    }

    public DialogViewModel(string topic, ILLMChatClient modelClient, IMapper mapper,
        GlobalOptions options, IRagSourceCollection ragSourceCollection,
        IList<IDialogItem>? items = null) : base(mapper, items)
    {
        // ActivatorUtilities.CreateInstance<>()
        _topic = topic;
        Requester = new RequesterViewModel(modelClient, NewRequest, options, ragSourceCollection, mapper)
        {
            Source = this
        };
        var functionTreeSelector = Requester.FunctionTreeSelector;
        functionTreeSelector.ConnectDefault()
            .ConnectSource(new ProxyFunctionGroupSource(() => this.SelectedFunctionGroups));
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
    }

    private void FunctionTreeSelectorOnAfterSelect()
    {
        this.SelectedFunctionGroups =
            this.Requester.FunctionTreeSelector.FunctionGroups.Where(tree => tree.IsSelected != false).ToArray();
        PopupBox.ClosePopupCommand.Execute(null, null);
    }

    public IEnumerable<IAIFunctionGroup> GetFunctionGroups()
    {
        if (SelectedFunctionGroups == null)
        {
            yield break;
        }

        foreach (var functionGroupTree in SelectedFunctionGroups)
        {
            functionGroupTree.RefreshCheckState();
            if (functionGroupTree.IsSelected != false)
            {
                yield return functionGroupTree;
            }
        }
    }
}