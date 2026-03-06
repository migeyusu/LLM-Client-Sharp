using ICSharpCode.AvalonEdit.Highlighting;
using LLMClient.Component.ViewModel.Base;
using Markdig.Helpers;

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

    public IHighlightingDefinition? SyntaxHighlighting
    {
        get
        {
            if (string.IsNullOrEmpty(Extension))
                return null;
            return HighlightingManager.Instance.GetDefinitionByExtension(Extension);
        }
    }

    public EditableCodeViewModel(StringLineGroup codeGroup, string? extension, string? name)
    {
        _codeGroup = codeGroup;
        _code = codeGroup.ToString();
        Extension = extension;
        Name = name;
    }
}