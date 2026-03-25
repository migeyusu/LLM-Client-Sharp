using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using LambdaConverters;
using LLMClient.Abstraction;
using LLMClient.Component.CustomControl;
using LLMClient.Component.UserControls;
using LLMClient.Component.ViewModel;
using MaterialDesignThemes.Wpf;
using Microsoft.Extensions.AI;

namespace LLMClient.Dialog.Models;

/// <summary>
/// 表示一个包含多个平行回复的对话项，通常用于展示同一请求的多个不同回复版本，供用户选择或比较。
/// </summary>
public class ParallelResponseViewItem : MultiResponseViewItem<ClientResponseViewItem>, ISearchableDialogItem,
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

    public override bool IsResponding
    {
        get { return Items.Any(item => item.IsResponding); }
        protected set { }
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
        get { return this.Items.Count > 1; }
    }

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

    public static ICommand SplitResponseCommand = new RelayCommand<ParallelResponseViewItem>((async multiResponse =>
    {
        if (multiResponse == null)
        {
            return;
        }

        //分裂回复集合为多个分支
        if (multiResponse.Items.Count < 2)
        {
            return;
        }

        //回复分裂准则：以当前接受的回复为主线，其他回复分裂为独立分支
        if (multiResponse.PreviousItem is RequestViewItem requestViewItem)
        {
            var acceptedResponse = multiResponse.AcceptedResponse;
            foreach (var responseViewItem in multiResponse.Items.ToArray())
            {
                if (responseViewItem != acceptedResponse)
                {
                    multiResponse.Items.Remove(responseViewItem);
                    requestViewItem.AppendChild(
                        new ParallelResponseViewItem([responseViewItem], multiResponse.ParentSession)
                            { InteractionId = multiResponse.InteractionId });
                }
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
    } = -1; //初始值必须为-1，AcceptedResponse会自动调整！

    public ClientResponseViewItem? AcceptedResponse
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

    public ParallelResponseViewItem(IEnumerable<ClientResponseViewItem> items, DialogSessionViewModel parentSession)
        : base(items, parentSession)
    {
        Items.CollectionChanged += (_, _) =>
        {
            this.ParentSession.IsDataChanged = true;
            OnPropertyChanged(nameof(IsMultiResponse));
        };
        SelectionPopup = new ModelSelectionPopupViewModel(client => { this.NewResponse(client.CreateClient()); })
        {
            SuccessRoutedCommand = PopupBox.ClosePopupCommand
        };
        RetryCommand = new RelayCommand<ClientResponseViewItem>(async void (responseViewItem) =>
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
        RemoveResponseCommand = new RelayCommand<ClientResponseViewItem>(o =>
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

    public override IEnumerable<ChatMessage> Messages
    {
        get
        {
            var responseMessages = AcceptedResponse?.Messages;
            return responseMessages ?? [];
        }
    }

    /*public Task<CompletedResult> InvokeRequest(ResponseViewItem responseViewItem)
    {
        var dialogItems = this.GetChatHistory().ToArray();
        var dialogContext = new DialogContext(dialogItems, this.ParentSession.SystemPrompt);
        return responseViewItem.ProcessRequest(dialogContext);
    }*/

    public Task<IResponse> NewResponse(ILLMChatClient chatClient, CancellationToken token = default)
    {
        var responseViewItem = new ClientResponseViewItem(chatClient);
        this.AddResponse(responseViewItem);
        return this.ProcessResponseItem(responseViewItem, token);
    }

    public void AddResponse(ClientResponseViewItem viewItem)
    {
        this.Items.Add(viewItem);
        this.AcceptedIndex = this.Items.Count - 1;
    }

    public void Insert(ClientResponseViewItem viewItem, int index)
    {
        this.Items.Insert(index, viewItem);
        this.AcceptedIndex = index;
    }

    public void RemoveResponse(ClientResponseViewItem viewItem)
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
    }

    public void RemoveAt(int index)
    {
        this.Items.RemoveAt(index);
        if (index >= this.Items.Count)
        {
            this.AcceptedIndex = this.Items.Count - 1;
        }
    }

    private async Task<IResponse> ProcessResponseItem(ClientResponseViewItem responseViewItem,
        CancellationToken token = default)
    {
        var dialogContext = DefaultDialogContextBuilder.CreateFromResponse(this, ParentSession.SystemPrompt);
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