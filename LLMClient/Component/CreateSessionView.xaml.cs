using System.Windows;
using System.Windows.Input;
using AutoMapper;
using CommunityToolkit.Mvvm.Input;
using LLMClient.Abstraction;
using LLMClient.Component.ViewModel;
using LLMClient.Component.ViewModel.Base;
using LLMClient.Configuration;
using LLMClient.Dialog;
using LLMClient.Endpoints;
using LLMClient.Project;
using LLMClient.Research;
using MaterialDesignThemes.Wpf;

namespace LLMClient.Component;

public partial class CreateSessionView
{
    public CreateSessionView()
    {
        InitializeComponent();
    }

    CreateSessionViewModel ViewModel
    {
        get { return (DataContext as CreateSessionViewModel)!; }
    }

    private void TreeView_OnSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        var newValue = e.NewValue;
        if (newValue is ILLMModel modelInfo)
        {
            ViewModel.ModelSelection.SelectedModel = modelInfo;
        }
    }
}

public class CreateSessionViewModel : BaseViewModel
{
    private readonly MainWindowViewModel _mainWindowViewModel;
    private int _selectedIndex;
    private string _dialogTitle = "新建会话";
    private bool _modelSelectionEnable = true;
    private ProjectViewModel _project;
    private IResearchCreationOption? _selectedCreationOption;

    public int SelectedIndex
    {
        get => _selectedIndex;
        set
        {
            if (value == _selectedIndex) return;
            _selectedIndex = value;
            OnPropertyChanged();
            ModelSelectionEnable = value != 2;
        }
    }

    public string DialogTitle
    {
        get => _dialogTitle;
        set
        {
            if (value == _dialogTitle) return;
            _dialogTitle = value;
            OnPropertyChanged();
        }
    }

    public bool ModelSelectionEnable
    {
        get => _modelSelectionEnable;
        set
        {
            if (value == _modelSelectionEnable) return;
            _modelSelectionEnable = value;
            OnPropertyChanged();
        }
    }

    public ModelSelectionViewModel ModelSelection { get; set; } = new();

    public ProjectViewModel Project
    {
        get => _project;
        set
        {
            if (Equals(value, _project)) return;
            _project = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CreateSessionCommand));
        }
    }

    public ICommand CreateSessionCommand => new RelayCommand(() =>
    {
        try
        {
            ILLMSession session;
            switch (SelectedIndex)
            {
                case 0:
                    var chatClient = ModelSelection.CreateClient();
                    session = _mainWindowViewModel.NewDialogViewModel(chatClient, this.DialogTitle);
                    break;
                case 1:
                    session = Project;
                    Project = _mainWindowViewModel.NewProjectViewModel();
                    break;
                /*case 2:
                    var researchClient = SelectedCreationOption?.CreateResearchClient();
                    if (researchClient == null)
                    {
                        return;
                    }

                    session = new ResearchSession(researchClient);*/
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            _mainWindowViewModel.AddSession(session);
            DialogHost.CloseDialogCommand.Execute(null, null);
        }
        catch (Exception e)
        {
            MessageBox.Show("Failed to create session: " + e.Message);
        }
    });

    public IReadOnlyList<IResearchCreationOption> ResearchCreationOptions { get; set; }

    public IResearchCreationOption? SelectedCreationOption
    {
        get => _selectedCreationOption;
        set
        {
            if (Equals(value, _selectedCreationOption)) return;
            _selectedCreationOption = value;
            OnPropertyChanged();
        }
    }

    public CreateSessionViewModel(MainWindowViewModel mainWindowViewModel,
        NvidiaResearchClientOption nvidiaResearchClientOption)
    {
        _mainWindowViewModel = mainWindowViewModel;
        _project = mainWindowViewModel.NewProjectViewModel();
        ResearchCreationOptions = new List<IResearchCreationOption>
        {
            nvidiaResearchClientOption
        };
    }
}