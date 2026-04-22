using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using LLMClient.Abstraction;
using LLMClient.Component.CustomControl;
using LLMClient.Component.ViewModel.Base;
using MaterialDesignThemes.Wpf;
using Microsoft.Extensions.AI;

namespace LLMClient.Dialog.Models;

public partial class LoopsPrunerViewModel : BaseViewModel
{
    public static RelayCommand<ResponseViewItemBase> CreatePruner { get; set; }
        = new(@base =>
        {
            if (@base == null)
            {
                return;
            }

            if (@base.Messages.Count() == 1)
            {
                MessageBoxes.Info("单条消息不需要管理");
                return;
            }

            DialogHost.Show(new LoopsPrunerViewModel(@base));
        });

    public ObservableCollection<ReactHistoryRound> Rounds { get; set; }

    private readonly ResponseViewItemBase _response;

    public LoopsPrunerViewModel(ResponseViewItemBase response)
    {
        this._response = response;
        Rounds =
            new ObservableCollection<ReactHistoryRound>(ChatMessageHierarchy.SegmentReactLevel(response.Messages.ToArray())
                .Rounds);
    }

    [RelayCommand]
    public void ApplyEdit()
    {
        _response.ApplyMessages(Rounds.SelectMany(round => round.Messages).ToArray());
        DialogHost.CloseDialogCommand.Execute(null, null);
    }

    [RelayCommand]
    public void RemoveMessage(ReactHistoryRound round)
    {
        Rounds.Remove(round);
    }
}