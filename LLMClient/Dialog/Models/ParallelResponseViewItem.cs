using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using LambdaConverters;
using LLMClient.Abstraction;
using LLMClient.Component.CustomControl;
using LLMClient.Component.UserControls;
using LLMClient.Component.ViewModel;
using LLMClient.Endpoints;
using MaterialDesignThemes.Wpf;
using Microsoft.Extensions.AI;

namespace LLMClient.Dialog.Models;

/// <summary>
/// 表示一个包含多个平行回复的对话项，通常用于展示同一请求的多个不同回复版本，供用户选择或比较。
/// </summary>
public class ParallelResponseViewItem : MultiResponseViewItem<DocResponseViewItem>, ISearchableDialogItem,
    IEditableDialogItem
{
    /// <summary>
    /// warning: 禁止用于绑定，因为没有实现属性通知
    /// </summary>
    [Bindable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public override bool IsAvailableInContext
    {
        get { return AcceptedResponse?.IsAvailableInContext == true; }
    }

    public void TriggerTextContentUpdate()
    {
        AcceptedResponse?.TriggerTextContentUpdate();
    }

    public bool IsResponding
    {
        get { return Items.Any(item => item.IsResponding); }
    }

    public bool HasAvailableMessage
    {
        get { return Items.Any(item => item.IsAvailableInContext); }
    }

    [Bindable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public override long Tokens
    {
        get { return AcceptedResponse?.Tokens ?? 0; }
    }

    public override ChatRole Role { get; } = ChatRole.Assistant;

    public bool IsMultiResponse
    {
        get;
        set
        {
            if (value == field) return;
            field = value;
            OnPropertyChanged();
        }
    } = false;

    [Bindable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public SearchableDocument? SearchableDocument
    {
        get { return AcceptedResponse?.SearchableDocument; }
    }

    public static readonly IMultiValueConverter DeleteItemConverter =
        MultiValueConverter.Create<bool, Visibility>(args =>
        {
            var values = args.Values;
            return !values[0] && values[1] && values[1] ? Visibility.Visible : Visibility.Collapsed;
        });

    public static ICommand CompareAllCommand { get; } = new RelayCommand<ParallelResponseViewItem>((item =>
    {
        if (item == null)
        {
            return;
        }

        if (!item.Items.Any())
        {
            return;
        }

        var compareWindow = new MultiResponseCompareWindow(item.Items);
        compareWindow.ShowDialog();
    }));

    public ModelSelectionPopupViewModel SelectionPopup { get; }

    public static ICommand SplitResponseCommand = new RelayCommand<ParallelResponseViewItem>((async mutiResponse =>
    {
        if (mutiResponse == null)
        {
            return;
        }

        //分裂回复集合为多个分支
        if (mutiResponse.Items.Count < 2)
        {
            return;
        }

        if (mutiResponse.HasFork)
        {
            await DialogHost.Show("只能对没有子节点的回复进行分裂");
            return;
        }

        if (mutiResponse.PreviousItem is RequestViewItem requestViewItem)
        {
            while (mutiResponse.Items.Count > 1)
            {
                var responseItem = mutiResponse.Items[1];
                mutiResponse.RemoveAt(1);
                var newMultiResponse = new ParallelResponseViewItem(mutiResponse.ParentSession)
                    { InteractionId = mutiResponse.InteractionId };
                newMultiResponse.AddResponse(responseItem);
                requestViewItem.AppendChild(newMultiResponse);
            }
        }
    }));

    public ICommand ClearOthersCommand { get; }

    public ICommand RetryCommand { get; }

    public int AcceptedIndex
    {
        get;
        set
        {
            if (value == field) return;
            field = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(AcceptedResponse));
            OnPropertyChanged(nameof(IsAvailableInContext));
            OnPropertyChanged(nameof(Tokens));
            OnPropertyChanged(nameof(SearchableDocument));
            CanGotoPrevious = value - 1 >= 0;
            CanGoToNext = value + 1 < Items.Count;
        }
    } = -1;

    public DocResponseViewItem? AcceptedResponse
    {
        get
        {
            if (AcceptedIndex < 0 || AcceptedIndex >= Items.Count)
            {
                return Items.FirstOrDefault();
            }

            return Items[AcceptedIndex];
        }
    }

    public bool CanGoToNext
    {
        get;
        set
        {
            if (value == field) return;
            field = value;
            OnPropertyChanged();
        }
    }

    public ICommand GotoNextCommand { get; }

    public bool CanGotoPrevious
    {
        get;
        set
        {
            if (value == field) return;
            field = value;
            OnPropertyChanged();
        }
    }

    public ICommand GotoPreviousCommand { get; }

    public ICommand RemoveResponseCommand { get; }

    public ParallelResponseViewItem(IEnumerable<DocResponseViewItem> items, DialogSessionViewModel parentSession)
        : base(items, parentSession)
    {
        Items.CollectionChanged += (_, _) =>
        {
            this.ParentSession.IsDataChanged = true;
            this.IsMultiResponse = Items.Count > 1;
        };
        IsMultiResponse = Items.Count > 1;
        SelectionPopup = new ModelSelectionPopupViewModel(client => { this.NewResponse(client.CreateClient()); })
        {
            SuccessRoutedCommand = PopupBox.ClosePopupCommand
        };
        RetryCommand = new RelayCommand<DocResponseViewItem>(async void (responseViewItem) =>
        {
            try
            {
                if (responseViewItem == null)
                {
                    return;
                }

                if (responseViewItem.IsResponding)
                {
                    return;
                }

                await this.ProcessResponseItem(responseViewItem, CancellationToken.None);
            }
            catch (Exception e)
            {
                MessageBoxes.Error(e.Message, "Error");
            }
        });
        RemoveResponseCommand = new RelayCommand<DocResponseViewItem>(o =>
        {
            if (o == null)
            {
                return;
            }

            if (Items.Count == 1)
            {
                return;
            }

            this.RemoveResponse(o);
        });

        ClearOthersCommand = new RelayCommand(() =>
        {
            var toRemove = Items.Where(item => item != AcceptedResponse).ToList();
            foreach (var item in toRemove)
            {
                this.RemoveResponse(item);
            }
        });
        GotoNextCommand = new RelayCommand(() =>
        {
            if (this.Items.Count == 0)
            {
                return;
            }

            var index = this.AcceptedResponse == null ? 0 : this.Items.IndexOf(this.AcceptedResponse);
            if (index < 0 || index + 1 >= this.Items.Count)
            {
                return;
            }

            this.AcceptedIndex = index + 1;
        });
        GotoPreviousCommand = new RelayCommand(() =>
        {
            if (this.Items.Count == 0)
            {
                return;
            }

            var index = this.AcceptedResponse == null ? 0 : this.Items.IndexOf(this.AcceptedResponse);
            if (index <= 0)
            {
                return;
            }

            this.AcceptedIndex = index - 1;
        });
    }

    public ParallelResponseViewItem(DialogSessionViewModel parentSession) : this([], parentSession)
    {
    }

    public override async IAsyncEnumerable<ChatMessage> GetMessagesAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var responseMessages = AcceptedResponse?.ResponseMessages;
        if (responseMessages == null)
        {
            yield break;
        }

        foreach (var chatMessage in responseMessages)
        {
            yield return chatMessage;
        }

        yield break;
    }

    /*public Task<CompletedResult> InvokeRequest(ResponseViewItem responseViewItem)
    {
        var dialogItems = this.GetChatHistory().ToArray();
        var dialogContext = new DialogContext(dialogItems, this.ParentSession.SystemPrompt);
        return responseViewItem.ProcessRequest(dialogContext);
    }*/

    public Task<IResponse> NewResponse(ILLMChatClient chatClient, CancellationToken token = default)
    {
        var responseViewItem = new DocResponseViewItem(chatClient);
        this.AddResponse(responseViewItem);
        return this.ProcessResponseItem(responseViewItem, token);
    }

    public void AddResponse(DocResponseViewItem viewItem)
    {
        this.Items.Add(viewItem);
        this.AcceptedIndex = this.Items.Count - 1;
        this.IsMultiResponse = Items.Count > 1;
    }

    public void Insert(DocResponseViewItem viewItem, int index)
    {
        this.Items.Insert(index, viewItem);
        this.AcceptedIndex = index;
    }

    public void RemoveResponse(DocResponseViewItem viewItem)
    {
        var indexOf = this.Items.IndexOf(viewItem);
        if (indexOf < 0)
        {
            return;
        }

        var removeAccepted = AcceptedResponse == viewItem;
        this.Items.RemoveAt(indexOf);
        if (removeAccepted)
        {
            this.AcceptedIndex = 0;
        }

        this.IsMultiResponse = Items.Count > 1;
    }

    public void RemoveAt(int index)
    {
        this.Items.RemoveAt(index);
        if (index >= this.Items.Count)
        {
            this.AcceptedIndex = this.Items.Count - 1;
        }

        this.IsMultiResponse = Items.Count > 1;
    }

    private async Task<IResponse> ProcessResponseItem(DocResponseViewItem responseViewItem,
        CancellationToken token = default)
    {
        var dialogItems = this.GetChatHistory().ToArray();
        var dialogContext = new DialogContext(dialogItems, ParentSession.SystemPrompt);
        ParentSession.RespondingCount++;
        try
        {
            await ParentSession.OnPreviewRequest(dialogContext, token);
            var completedResult = await responseViewItem.Process(dialogContext, token);
            ParentSession.OnResponseCompleted(completedResult);
            return completedResult;
        }
        finally
        {
            ParentSession.RespondingCount--;
        }
    }
}