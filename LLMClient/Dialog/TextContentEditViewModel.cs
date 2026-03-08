using System.Windows.Input;
using LLMClient.Component.ViewModel.Base;
using Microsoft.Extensions.AI;
using Microsoft.Xaml.Behaviors.Core;

namespace LLMClient.Dialog;

public abstract class TextContentEditViewModel : BaseViewModel
{
    public ICommand RollbackCommand { get; }

    protected readonly TextContent TextContent;

    public string? MessageId { get; }

    private bool _hasEdit;

    public bool HasEdit
    {
        get => _hasEdit;
        set
        {
            if (value == _hasEdit) return;
            _hasEdit = value;
            OnPropertyChanged();
        }
    }

    protected TextContentEditViewModel(TextContent textContent, string? messageId)
    {
        this.TextContent = textContent;
        this.MessageId = messageId;
        RollbackCommand = new ActionCommand(Rollback);
    }

    protected abstract void Rollback();

    public abstract bool Check();

    public abstract Task ApplyText();
}

