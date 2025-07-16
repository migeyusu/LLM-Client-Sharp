// #define TESTMODE

using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Input;
using LLMClient.Abstraction;
using LLMClient.Data;
using LLMClient.UI.Component;
using MaterialDesignThemes.Wpf;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Xaml.Behaviors.Core;
using MessageBox = System.Windows.MessageBox;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;

namespace LLMClient.UI.Dialog;

public class DialogViewModel : DialogSessionViewModel
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
            .Where(item => item is RequestViewItem || item is EraseViewItem)
            .ToArray();
        ChainStepCount = pendingItems.Length;
        try
        {
            foreach (var oldDialogDialogItem in pendingItems)
            {
                if (oldDialogDialogItem is RequestViewItem requestViewItem)
                {
                    var newGuid = Guid.NewGuid();
                    var newItem = requestViewItem.Clone();
                    DialogItems.Add(newItem);
                    var copy = GenerateHistory();
                    int retryCount = 3;
                    while (retryCount > 0)
                    {
                        var multiResponseViewItem = new MultiResponseViewItem(this) { InteractionId = newGuid };
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

    public IPromptsResource PromptsResource
    {
        get { return ServiceLocator.GetService<IPromptsResource>()!; }
    }

    private bool _isChaining;
    private int _chainStepCount;
    private int _chainingStep;

    private readonly string[] _notTrackingProperties =
    [
        nameof(ScrollViewItem),
        nameof(SearchText)
    ];

    public RequesterViewModel Requester { get; }

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
        /*stringBuilder.AppendLine($"# {this.Topic}");
        stringBuilder.AppendLine($"### {this.DefaultClient.Name}");*/
        foreach (var viewItem in DialogItems.Where((item => item.IsAvailableInContext)))
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

    #region core method

    public async void ReBaseOn(RequestViewItem redoItem)
    {
        RemoveAfter(redoItem);
        await Requester.GetResponse(redoItem);
    }

    public async void ClearBefore(RequestViewItem requestViewItem)
    {
        if ((await DialogHost.Show(new ConfirmView() { Header = "清空会话？" })) is true)
        {
            RemoveBefore(requestViewItem);
        }
    }

    #endregion

    public DialogViewModel(string topic, ILLMClient modelClient, IList<IDialogItem>? items = null)
        : base(items)
    {
        _topic = topic;
        Requester = new RequesterViewModel(modelClient, SendRequestCore);
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
}