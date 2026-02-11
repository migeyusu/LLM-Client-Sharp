using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using LLMClient.Component.ViewModel.Base;

namespace LLMClient.Component.Render;

public class CodeCompareViewModel : BaseViewModel
{
    public static CodeCompareViewModel Instance { get; } = new();

    public static ICommand AddToCompareCommand = new RelayCommand<CodeViewModel>((o) =>
    {
        if (o != null)
        {
            Instance.AddCode(o);
        }
    });

    private readonly ObservableCollection<CodeViewModel> _codeViewModels = new();
    private bool _hasMember;

    public ReadOnlyObservableCollection<CodeViewModel> PendingList { get; }

    public bool HasMember
    {
        get => _hasMember;
        set
        {
            if (value == _hasMember) return;
            _hasMember = value;
            OnPropertyChanged();
        }
    }

    public string? ComparedLeft
    {
        get
        {
            if (_codeViewModels.Count > 0)
            {
                return _codeViewModels[0].CodeString;
            }

            return null;
        }
    }

    public string? ComparedRight
    {
        get
        {
            if (_codeViewModels.Count > 1)
            {
                return _codeViewModels[1].CodeString;
            }

            return null;
        }
    }

    public ICommand RemoveFromCompareCommand { get; }

    public ICommand ClearCommand { get; }

    public CodeCompareViewModel()
    { 
        RemoveFromCompareCommand = new RelayCommand(() =>
        {
            if (_codeViewModels.Count > 0)
            {
                _codeViewModels.RemoveAt(_codeViewModels.Count - 1);
            }

            if (_codeViewModels.Count == 0)
            {
                HasMember = false;
            }
            OnPropertyChanged(nameof(ComparedLeft));
            OnPropertyChanged(nameof(ComparedRight));
            
        });
        PendingList = new ReadOnlyObservableCollection<CodeViewModel>(_codeViewModels);
        ClearCommand = new RelayCommand(() =>
        {
            _codeViewModels.Clear();
            HasMember = false;
            OnPropertyChanged(nameof(ComparedLeft));
            OnPropertyChanged(nameof(ComparedRight));
        });
    }

    public void AddCode(CodeViewModel model)
    {
        if (_codeViewModels.Count == 2)
        {
            _codeViewModels.RemoveAt(0);
        }

        _codeViewModels.Add(model);
        HasMember = true;
        OnPropertyChanged(nameof(ComparedLeft));
        OnPropertyChanged(nameof(ComparedRight));
    }
}