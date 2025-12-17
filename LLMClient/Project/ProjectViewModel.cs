using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows.Input;
using AutoMapper;
using CommunityToolkit.Mvvm.Input;
using LLMClient.Abstraction;
using LLMClient.Component.Utility;
using LLMClient.Component.ViewModel;
using LLMClient.Configuration;
using LLMClient.Data;
using LLMClient.Dialog;
using LLMClient.Endpoints;
using LLMClient.ToolCall;
using LLMClient.ToolCall.Servers;
using MaterialDesignThemes.Wpf;
using Microsoft.Xaml.Behaviors.Core;

namespace LLMClient.Project;

public class ProjectViewModel : FileBasedSessionBase, ILLMSessionLoader<ProjectViewModel>, IPromptableSession
{
    public const string SaveDir = "Projects";

    private IMapper _mapper;

    private bool _isDataChanged = true;

    public override bool IsDataChanged
    {
        get { return Tasks.Any(task => task.IsDataChanged) || Requester.IsDataChanged || _isDataChanged; }
        set
        {
            _isDataChanged = value;
            if (!value)
            {
                foreach (var projectTask in this.Tasks)
                {
                    projectTask.IsDataChanged = value;
                }

                Requester.IsDataChanged = value;
            }
        }
    }

    public override bool IsBusy
    {
        get { return Tasks.Any(task => task.IsBusy); }
    }

    private static readonly Lazy<string> SaveFolderPathLazy = new(() => Path.GetFullPath(SaveDir));

    public static string SaveFolderPath => SaveFolderPathLazy.Value;

    protected override string DefaultSaveFolderPath
    {
        get { return SaveFolderPathLazy.Value; }
    }

    public RequesterViewModel Requester { get; }

    #region file

    public static async Task<ProjectViewModel?> LoadFromStream(Stream fileStream, IMapper mapper)
    {
        try
        {
            var persistModel =
                await JsonSerializer.DeserializeAsync<ProjectPersistModel>(fileStream, SerializerOption);
            if (persistModel == null)
            {
                return null;
            }

            if (persistModel.Version != ProjectPersistModel.CurrentVersion)
            {
                return null;
            }

            var viewModel = mapper.Map<ProjectPersistModel, ProjectViewModel>(persistModel, _ => { });
            viewModel.IsDataChanged = false;
            return viewModel;
        }
        catch (Exception e)
        {
            Trace.TraceError(e.ToString());
            return null;
        }
    }

    private string? _description = string.Empty;
    private string? _folderPath;
    private string? _name;

    protected override async Task SaveToStream(Stream stream)
    {
        var dialogModel = _mapper.Map<ProjectViewModel, ProjectPersistModel>(this, _ => { });
        await JsonSerializer.SerializeAsync(stream, dialogModel, SerializerOption);
    }

    public override object Clone()
    {
        var projectPersistModel = _mapper.Map<ProjectViewModel, ProjectPersistModel>(this, _ => { });
        var cloneProject = _mapper.Map<ProjectPersistModel, ProjectViewModel>(projectPersistModel, _ => { });
        cloneProject.IsDataChanged = true;
        return cloneProject;
    }


    private void TagDataChangedOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        IsDataChanged = true;
    }

    private void TrackClientChanged(ILLMChatClient client)
    {
        if (client is INotifyPropertyChanged newValue)
        {
            newValue.PropertyChanged += TagDataChangedOnPropertyChanged;
        }

        if (client.Parameters is INotifyPropertyChanged newParameters)
        {
            newParameters.PropertyChanged += TagDataChangedOnPropertyChanged;
        }
    }

    #endregion

    #region info

    public string? Name
    {
        get => _name;
        set
        {
            if (value == _name) return;
            _name = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// sample:this is a *** project
    /// </summary>
    public string? Description
    {
        get => _description;
        set
        {
            if (value == _description) return;
            this.ClearError();
            if (string.IsNullOrEmpty(value))
            {
                this.AddError("Description cannot be null or empty.");
                return;
            }

            _description = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Context));
            OnPropertyChanged(nameof(UserSystemPrompt));
        }
    }


    public string? UserSystemPrompt
    {
        get => Description;
        set => Description = value;
    }

    private ObservableCollection<PromptEntry> _extendedSystemPrompts = [];

    public ObservableCollection<PromptEntry> ExtendedSystemPrompts
    {
        get => _extendedSystemPrompts;
        set
        {
            if (Equals(value, _extendedSystemPrompts)) return;
            _extendedSystemPrompts.CollectionChanged -= ExtendedSystemPromptsOnCollectionChanged;
            _extendedSystemPrompts = value;
            value.CollectionChanged += ExtendedSystemPromptsOnCollectionChanged;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Context));
        }
    }


    private readonly StringBuilder _systemPromptBuilder = new StringBuilder(1024);

    /// <summary>
    /// 项目级别的上下文，在task间共享
    /// </summary>
    public string? Context
    {
        get
        {
            if (!Check())
            {
                return null;
            }

            _systemPromptBuilder.Clear();
            foreach (var promptEntry in ExtendedSystemPrompts)
            {
                _systemPromptBuilder.AppendLine(promptEntry.Prompt);
            }

            _systemPromptBuilder.AppendFormat("我正在开发一个名为{0}的软件项目，项目代码位于文件夹{1}。", Name, FolderPath);
            _systemPromptBuilder.AppendLine();
            _systemPromptBuilder.AppendLine("项目背景/描述如下：");
            _systemPromptBuilder.AppendLine(Description);
            var contextTasks = this.Tasks
                .Where(model => model.EnableInContext && string.IsNullOrEmpty(model.Summary))
                .ToArray();
            if (contextTasks.Any())
            {
                _systemPromptBuilder.AppendLine("以下是与任务相关的信息：");
                foreach (var projectTaskViewModel in contextTasks)
                {
                    _systemPromptBuilder.Append("#");
                    _systemPromptBuilder.AppendLine(projectTaskViewModel.Name);
                    _systemPromptBuilder.AppendLine(projectTaskViewModel.Summary);
                }
            }

            return _systemPromptBuilder.ToString();
        }
    }

    public ICommand SelectProjectFolderCommand => new RelayCommand(() =>
    {
        var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "请选择项目文件夹",
            SelectedPath = string.IsNullOrEmpty(FolderPath)
                ? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
                : FolderPath
        };

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            FolderPath = dialog.SelectedPath;
        }
    });

    public ICommand AddAllowedFolderPathsCommand => new RelayCommand(() =>
    {
        var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "请选择允许的文件夹路径",
            SelectedPath = string.IsNullOrEmpty(FolderPath)
                ? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
                : FolderPath
        };

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            var selectedPath = dialog.SelectedPath;
            if (!AllowedFolderPaths.Contains(selectedPath))
            {
                AllowedFolderPaths.Add(selectedPath);
            }
        }
    });

    public ICommand RemoveAllowedFolderPathCommand => new ActionCommand((o) =>
    {
        if (o is string s)
        {
            AllowedFolderPaths.Remove(s);
        }
    });

    public ObservableCollection<string> AllowedFolderPaths { get; set; } = new ObservableCollection<string>();

    /// <summary>
    /// 项目路径，项目所在文件夹路径
    /// </summary>
    public string? FolderPath
    {
        get => _folderPath;
        set
        {
            if (value == _folderPath) return;
            this.ClearError();
            if (string.IsNullOrEmpty(value))
            {
                this.AddError("FolderPath cannot be null or empty.");
                return;
            }

            if (!Directory.Exists(value))
            {
                this.AddError("FolderPath does not exist.");
            }

            if (!string.IsNullOrEmpty(_folderPath))
            {
                AllowedFolderPaths.Remove(_folderPath);
            }

            _folderPath = value;
            AllowedFolderPaths.Add(value);
            OnPropertyChanged();
            OnPropertyChanged(nameof(Context));
        }
    }

    public virtual string Type { get; } = "Standard";

    private long _tokensConsumption;

    public long TokensConsumption
    {
        get => _tokensConsumption;
        set
        {
            if (value == _tokensConsumption) return;
            _tokensConsumption = value;
            OnPropertyChanged();
        }
    }

    private double _totalPrice;

    public double TotalPrice
    {
        get => _totalPrice;
        set
        {
            if (value.Equals(_totalPrice)) return;
            _totalPrice = value;
            OnPropertyChanged();
        }
    }

    private IAIFunctionGroup[] _allowedFunctions = [new FileSystemPlugin(), new WinCLIPlugin()];

    #endregion

    #region tasks

    public ICommand NewTaskCommand => new ActionCommand(_ => { AddTask(new ProjectTaskViewModel(this, _mapper)); });

    public void AddTask(ProjectTaskViewModel task)
    {
        task.PropertyChanged += OnTaskPropertyChanged;
        this.Tasks.Add(task);
    }

    public void RemoveTask(ProjectTaskViewModel task)
    {
        task.PropertyChanged -= OnTaskPropertyChanged;
        this.Tasks.Remove(task);
    }

    private void OnTaskPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        var propertyName = e.PropertyName;
        switch (propertyName)
        {
            case nameof(ProjectTaskViewModel.EnableInContext):
                OnPropertyChanged(nameof(Context));
                break;
            case nameof(DialogSessionViewModel.IsBusy):
                OnPropertyChanged(nameof(IsBusy));
                break;
        }
    }

    public ObservableCollection<ProjectTaskViewModel> Tasks { get; set; }

    private ProjectTaskViewModel? _selectedTask;

    public ProjectTaskViewModel? SelectedTask
    {
        get => _selectedTask;
        set
        {
            if (Equals(value, _selectedTask)) return;
            _selectedTask = value;
            OnPropertyChanged();
            Requester.FunctionGroupSource = value;
            Requester.FunctionTreeSelector.Reset();
        }
    }

    #endregion

    private readonly string[] _notTrackingProperties =
    [
        nameof(EditTime),
        nameof(SelectedTask)
    ];

    public ProjectViewModel(ILLMChatClient modelClient, IMapper mapper, GlobalOptions options,
        IRagSourceCollection ragSourceCollection, IEnumerable<ProjectTaskViewModel>? tasks = null)
    {
        _mapper = mapper;
        Requester = new RequesterViewModel(modelClient, GetResponse, options, ragSourceCollection, mapper)
        {
            FunctionTreeSelector =
            {
                FunctionSelected = true,
            }
        };
        var functionTreeSelector = Requester.FunctionTreeSelector;
        functionTreeSelector.ConnectDefault()
            .ConnectSource(new ProxyFunctionGroupSource(() => this.SelectedTask?.SelectedFunctionGroups));
        functionTreeSelector.AfterSelect += FunctionTreeSelectorOnAfterSelect;
        Requester.RequestCompleted += response =>
        {
            this.TokensConsumption += response.Tokens;
            this.TotalPrice += (response.Price ?? 0);
        };
        this.Tasks = [];
        if (tasks != null)
        {
            foreach (var projectTask in tasks)
            {
                this.AddTask(projectTask);
            }
        }

        this.PropertyChanged += (_, e) =>
        {
            var propertyName = e.PropertyName;
            if (_notTrackingProperties.Contains(propertyName))
            {
                return;
            }

            this.EditTime = DateTime.Now;
            IsDataChanged = true;
        };
        Tasks.CollectionChanged += OnCollectionChanged;
        _extendedSystemPrompts.CollectionChanged += ExtendedSystemPromptsOnCollectionChanged;
    }

    private void ExtendedSystemPromptsOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        this.IsDataChanged = true;
        OnPropertyChanged(nameof(Context));
    }

    private void FunctionTreeSelectorOnAfterSelect()
    {
        if (this.SelectedTask == null)
        {
            return;
        }

        this.SelectedTask.SelectedFunctionGroups =
            this.Requester.FunctionTreeSelector.FunctionGroups.Where(tree => tree.IsSelected != false)
                .Select((tree => (CheckableFunctionGroupTree)tree.Clone())).ToArray();
        PopupBox.ClosePopupCommand.Execute(null, null);
    }

    private Task<CompletedResult> GetResponse(ILLMChatClient arg1, IRequestItem arg2, int? index = null)
    {
        if (SelectedTask == null)
        {
            return Task.FromException<CompletedResult>(new NotSupportedException("未选择任务"));
        }

        if (!SelectedTask.Validate())
        {
            return Task.FromException<CompletedResult>(new InvalidOperationException("当前任务配置不合法"));
        }

        if (!this.Check())
        {
            return Task.FromException<CompletedResult>(new InvalidOperationException("当前项目配置不合法"));
        }

        this.Ready();
        return SelectedTask.NewRequest(arg1, arg2, index);
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        this.IsDataChanged = true;
    }

    public bool Check()
    {
        if (this.HasErrors)
        {
            return false;
        }

        if (string.IsNullOrEmpty(Name))
        {
            this.AddError("Name cannot be null or empty.", nameof(Name));
        }

        if (string.IsNullOrEmpty(FolderPath))
        {
            this.AddError("FolderPath cannot be null or empty.", nameof(FolderPath));
        }

        if (string.IsNullOrEmpty(Description))
        {
            this.AddError("Description cannot be null or empty.", nameof(Description));
        }

        if (!AllowedFolderPaths.Any())
        {
            MessageEventBus.Publish("AllowedFolderPaths cannot be empty.");
            return false;
        }

        return !this.HasErrors;
    }

    public void Ready()
    {
        var functionGroups = this.SelectedTask?.SelectedFunctionGroups;
        if (functionGroups != null)
        {
            foreach (var aiFunctionGroup in functionGroups)
            {
                if (aiFunctionGroup.Data is FileSystemPlugin fileSystemPlugin)
                {
                    fileSystemPlugin.BypassPaths = AllowedFolderPaths;
                }
            }
        }
    }

    public void ForkPreTask(IDialogItem? dialogViewItem)
    {
        var projectTaskViewModel = this.SelectedTask;
        if (projectTaskViewModel == null)
        {
            return;
        }

        var dialogSessionClone =
            _mapper.Map<ProjectTaskViewModel, ProjectTaskPersistModel>(projectTaskViewModel, _ => { });
        if (dialogSessionClone == null)
        {
            return;
        }

        if (dialogViewItem != null)
        {
            var findIndex = projectTaskViewModel.DialogItems.IndexOf(dialogViewItem);
            if (findIndex >= 0)
            {
                dialogSessionClone.DialogItems = dialogSessionClone.DialogItems?.Take(findIndex + 1).ToArray();
            }
        }

        var cloneSession =
            _mapper.Map<ProjectTaskPersistModel, ProjectTaskViewModel>(dialogSessionClone,
                (options => { options.Items[AutoMapModelTypeConverter.ParentProjectViewModelKey] = this; }));
        cloneSession.IsDataChanged = true;
        this.AddTask(cloneSession);
    }
}