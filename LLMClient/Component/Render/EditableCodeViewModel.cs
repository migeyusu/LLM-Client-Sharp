using System.Windows.Documents;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using ICSharpCode.AvalonEdit.Highlighting;
using LLMClient.Component.ViewModel.Base;
using Markdig.Helpers;
using Microsoft.Xaml.Behaviors.Core;

namespace LLMClient.Component.Render;

public class EditableCodeViewModel : BaseViewModel
{
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

    private string? _fileLocation;

    public string? FileLocation
    {
        get => _fileLocation;
        set
        {
            if (_fileLocation == value) return;
            _fileLocation = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(DisplayHeader));
        }
    }

    public string DisplayHeader => !string.IsNullOrEmpty(FileLocation) ? $"{FileLocation}({Name})" : $"{Extension}/{Name}";

    public IHighlightingDefinition? SyntaxHighlighting
    {
        get
        {
            if (string.IsNullOrEmpty(Extension))
                return null;
            return HighlightingManager.Instance.GetDefinitionByExtension(Extension);
        }
    }

    public ICommand DeleteCommand { get; }

    public ICommand RollbackCommand { get; }

    public EditableCodeViewModel(StringLineGroup codeGroup, string? extension, string? name)
        : this(codeGroup.ToString(), extension, name)
    {
    }

    public EditableCodeViewModel(string code, string? extension, string? name)
    {
        _code = code;
        Extension = extension;
        Name = name;
        RollbackCommand = new ActionCommand(o => { Code = code; });
        DeleteCommand = new RelayCommand<BlockUIContainer>(o =>
        {
            if (o == null)
            {
                return;
            }

            if (o.Parent is FlowDocument document)
            {
                document.Blocks.Remove(o);
            }
        });
    }
    
    
}