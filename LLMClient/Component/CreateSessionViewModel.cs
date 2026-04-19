using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using LLMClient.Agent.Research;
using LLMClient.Component.CustomControl;
using LLMClient.Component.ViewModel;
using LLMClient.Component.ViewModel.Base;
using LLMClient.Dialog;
using LLMClient.Project;
using MaterialDesignThemes.Wpf;

namespace LLMClient.Component;

public class CreateSessionViewModel : BaseViewModel
{
    public int SelectedIndex
    {
        get;
        set
        {
            if (value == field) return;
            field = value;
            OnPropertyChanged();
            ModelSelectionEnable = value != 2;
        }
    }

    public string DialogTitle
    {
        get;
        set
        {
            if (value == field) return;
            field = value;
            OnPropertyChanged();
        }
    } = "新建会话";

    public bool ModelSelectionEnable
    {
        get;
        set
        {
            if (value == field) return;
            field = value;
            OnPropertyChanged();
        }
    } = true;

    public ModelSelectionViewModel ModelSelection { get; set; } = new();

    public ProjectOption Project
    {
        get;
        set
        {
            if (Equals(value, field)) return;
            field = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CreateSessionCommand));
        }
    } = new() { TypeEditable = true };

    public ICommand CreateSessionCommand { get; }

    public CreateSessionViewModel(IViewModelFactory factory, MainWindowViewModel mainWindowViewModel)
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
                        session = factory.CreateViewModel<DialogFileViewModel>(this.DialogTitle, string.Empty,
                            chatClient);
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
                                session = factory.CreateViewModel<CSharpProjectViewModel>(projectOption, string.Empty,
                                    client);
                                break;
                            case ProjectType.Cpp:
                                session = factory.CreateViewModel<CppProjectViewModel>(projectOption, string.Empty,
                                    client);
                                break;
                            default:
                                throw new NotSupportedException();
                        }

                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                mainWindowViewModel.AddSession(session);
                DialogHost.CloseDialogCommand.Execute(null, null);
            }
            catch (Exception e)
            {
                MessageBoxes.Error("Failed to create session: " + e.Message);
            }
        });
    }
}