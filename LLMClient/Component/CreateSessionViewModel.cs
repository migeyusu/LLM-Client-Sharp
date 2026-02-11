using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using LLMClient.Abstraction;
using LLMClient.Component.ViewModel;
using LLMClient.Component.ViewModel.Base;
using LLMClient.Dialog;
using LLMClient.Project;
using LLMClient.Research;
using MaterialDesignThemes.Wpf;

namespace LLMClient.Component;

public class CreateSessionViewModel : BaseViewModel
{
    private int _selectedIndex;
    private string _dialogTitle = "新建会话";
    private bool _modelSelectionEnable = true;
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

    private ProjectOption _project = new() { TypeEditable = true };

    public ProjectOption Project
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

    public ICommand CreateSessionCommand { get; }

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

    public CreateSessionViewModel(IViewModelFactory factory, MainWindowViewModel mainWindowViewModel,
        NvidiaResearchClientOption nvidiaResearchClientOption)
    {
        CreateSessionCommand = new RelayCommand(() =>
        {
            try
            {
                FileBasedSessionBase session;
                switch (SelectedIndex)
                {
                    case 0:
                        var chatClient = ModelSelection.CreateClient();
                        session = factory.CreateViewModel<DialogFileViewModel>(this.DialogTitle, chatClient);
                        break;
                    case 1:
                        if (!Project.Check())
                        {
                            throw new NotSupportedException("Project option is not valid");
                        }

                        var client = ModelSelection.CreateClient();
                        var projectOption = (ProjectOption)Project.Clone();

                        switch (projectOption.Type)
                        {
                            case ProjectType.CSharp:
                                session = factory.CreateViewModel<CSharpProjectViewModel>(projectOption, client);
                                break;
                            case ProjectType.Default:
                                session = factory.CreateViewModel<GeneralProjectViewModel>(projectOption, client);
                                break;
                            case ProjectType.Cpp:
                                session = factory.CreateViewModel<CppProjectViewModel>(projectOption, client);
                                break;
                            default:
                                throw new NotSupportedException();
                            // _mainWindowViewModel.NewProjectViewModel(client, projectOption);
                        }

                        break;
                    case 2:
                        var researchClient = SelectedCreationOption?.CreateResearchClient();
                        if (researchClient == null)
                        {
                            return;
                        }

                        session = new ResearchSession(researchClient);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                mainWindowViewModel.AddSession(session);
                DialogHost.CloseDialogCommand.Execute(null, null);
            }
            catch (Exception e)
            {
                MessageBox.Show("Failed to create session: " + e.Message);
            }
        });
        ResearchCreationOptions = new List<IResearchCreationOption>
        {
            nvidiaResearchClientOption
        };
    }
}