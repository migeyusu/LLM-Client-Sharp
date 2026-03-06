using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using ICSharpCode.AvalonEdit.Highlighting;
using LLMClient.Component.ViewModel.Base;
using Markdig.Helpers;
using Microsoft.Xaml.Behaviors.Core;

namespace LLMClient.Component.Render;

public class EditableCodeViewModel : BaseViewModel
{
    private readonly StringLineGroup _codeGroup;
    private string _code;

    public string Code
    {
        get => _code;
        set
        {
            if (value == _code) return;
            _code = value;
            OnPropertyChanged();
        }
    }

    public string? Extension { get; }

    public string? Name { get; }

    public string? FileLocation { get; set; }

    public IHighlightingDefinition? SyntaxHighlighting
    {
        get
        {
            if (string.IsNullOrEmpty(Extension))
                return null;
            return HighlightingManager.Instance.GetDefinitionByExtension(Extension);
        }
    }

    /*public ICommand DeleteCommand { get; }*/

    public ICommand RollbackCommand { get; }
    
    public EditableCodeViewModel(StringLineGroup codeGroup, string? extension, string? name)
    {
        _codeGroup = codeGroup;
        _code = codeGroup.ToString();
        Extension = extension;
        Name = name;
        RollbackCommand = new ActionCommand(o => { Code = _codeGroup.ToString(); });
    }
}