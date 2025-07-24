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
using LLMClient.Data;
using LLMClient.Endpoints;
using LLMClient.UI.Component;
using LLMClient.UI.Dialog;
using LLMClient.UI.MCP;
using LLMClient.UI.MCP.Servers;
using MaterialDesignThemes.Wpf;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Xaml.Behaviors.Core;

namespace LLMClient.UI.Project;

public class ProjectViewModel : FileBasedSessionBase
{
    public const string SaveDir = "Projects";

    private static IMapper Mapper => ServiceLocator.GetService<IMapper>()!;

    private bool _isDataChanged = true;

    public override bool IsDataChanged
    {
        get { return Tasks.Any(task => task.IsDataChanged) || _isDataChanged; }
        set
        {
            _isDataChanged = value;
            if (!value)
            {
                foreach (var projectTask in this.Tasks)
                {
                    projectTask.IsDataChanged = value;
                }
            }
        }
    }

    public override bool IsBusy { get; } = false;

    private static readonly Lazy<string> SaveFolderPathLazy = new Lazy<string>((() => Path.GetFullPath(SaveDir)));

    public static string SaveFolderPath => SaveFolderPathLazy.Value;

    protected override string DefaultSaveFolderPath
    {
        get { return SaveFolderPathLazy.Value; }
    }

    public ProjectConfigViewModel ConfigViewModel { get; }

    public RequesterViewModel Requester { get; }

    #region file

    public static async Task<ProjectViewModel?> LoadFromFile(FileInfo fileInfo,
        int version = ProjectPersistModel.CurrentVersion)
    {
        if (!fileInfo.Exists)
        {
            return null;
        }

        try
        {
            await using (var fileStream = fileInfo.OpenRead())
            {
                var persistModel =
                    await JsonSerializer.DeserializeAsync<ProjectPersistModel>(fileStream, SerializerOption);
                if (persistModel == null)
                {
                    Trace.TraceError($"加载会话{fileInfo.FullName}失败：文件内容为空");
                    return null;
                }

                if (persistModel.Version != version)
                {
                    Trace.TraceError($"加载会话{fileInfo.FullName}失败：版本不匹配");
                    return null;
                }

                var viewModel = Mapper.Map<ProjectPersistModel, ProjectViewModel>(persistModel, (options => { }));
                viewModel.FileFullPath = fileInfo.FullName;
                viewModel.IsDataChanged = false;
                return viewModel;
            }
        }
        catch (Exception e)
        {
            Trace.TraceError(e.ToString());
            return null;
        }
    }

    public static async IAsyncEnumerable<ProjectViewModel> LoadFromLocal()
    {
        string fullPath = SaveFolderPathLazy.Value;
        var directoryInfo = new DirectoryInfo(fullPath);
        if (!directoryInfo.Exists)
        {
            directoryInfo.Create();
        }

        foreach (var fileInfo in directoryInfo.GetFiles())
        {
            var dialogViewModel = await LoadFromFile(fileInfo);
            if (dialogViewModel == null)
            {
                continue;
            }

            yield return dialogViewModel;
        }
    }

    public static async IAsyncEnumerable<ProjectViewModel> ImportFiles(IEnumerable<FileInfo> fileInfos)
    {
        var targetFolderPath = SaveFolderPathLazy.Value;
        var targetDirectoryInfo = new DirectoryInfo(targetFolderPath);
        if (!targetDirectoryInfo.Exists)
        {
            targetDirectoryInfo.Create();
        }

        foreach (var fileInfo in fileInfos)
        {
            if (fileInfo.DirectoryName?.Equals(targetDirectoryInfo.FullName, StringComparison.OrdinalIgnoreCase)
                == false)
            {
                ProjectViewModel? projectViewModel = null;
                try
                {
                    var newFilePath = Path.Combine(targetDirectoryInfo.FullName, fileInfo.Name);
                    if (File.Exists(newFilePath))
                    {
                        MessageEventBus.Publish($"会话文件 {fileInfo.Name} 已存在，未进行复制。");
                    }
                    else
                    {
                        File.Copy(fileInfo.FullName, newFilePath, true);
                        var info = new FileInfo(newFilePath);
                        projectViewModel = await LoadFromFile(info);
                    }
                }
                catch (Exception e)
                {
                    MessageEventBus.Publish("导入出现问题" + e.Message);
                    continue;
                }

                if (projectViewModel == null) continue;
                yield return projectViewModel;
            }
        }
    }

    private string? _description = string.Empty;
    private IList<string>? _languageNames;
    private string? _folderPath;
    private string? _name;

    protected override async Task SaveToStream(Stream stream)
    {
        var dialogModel = Mapper.Map<ProjectViewModel, ProjectPersistModel>(this, (options => { }));
        await JsonSerializer.SerializeAsync(stream, dialogModel, SerializerOption);
    }

    private void TagDataChangedOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        IsDataChanged = true;
    }

    private void TrackClientChanged(ILLMClient client)
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
        }
    }

    public IList<string>? LanguageNames
    {
        get => _languageNames;
        set
        {
            if (Equals(value, _languageNames)) return;
            this.ClearError();
            if (value == null)
            {
                AddError("LanguageNames cannot be null.");
                return;
            }

            _languageNames = value;
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
            Debug.Assert(LanguageNames != null, nameof(LanguageNames) + " != null");
            _systemPromptBuilder.AppendFormat("我正在Windows上使用语言{0}上开发位于{1}的一个软件项目。",
                string.Join(",", LanguageNames), FolderPath);
            _systemPromptBuilder.AppendLine();
            _systemPromptBuilder.AppendLine(Description);
            return _systemPromptBuilder.ToString();
        }
    }

    private AIFunctionSelectorViewModel _functionSelector = new((() =>
    {
        DialogHost.CloseDialogCommand.Execute(true, null);
    }));

    public ICommand ChooseFunctionsCommand => new RelayCommand(async () =>
    {
        _functionSelector.ResetSource()
            .EnsureAsync();
        _functionSelector.SelectedFunctions = this.AllowedFunctions;
        if (await DialogHost.Show(_functionSelector) is true)
        {
            this.AllowedFunctions = _functionSelector.SelectedFunctions.Select((group =>
            {
                if (group is IBuiltInFunctionGroup kernelFunctionGroup)
                {
                    return (kernelFunctionGroup.Clone() as IBuiltInFunctionGroup)!;
                }

                return group;
            })).ToArray();
        }
    });

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

    public IAIFunctionGroup[] AllowedFunctions
    {
        get => _allowedFunctions;
        set
        {
            if (Equals(value, _allowedFunctions)) return;
            _allowedFunctions = value;
            OnPropertyChanged();
        }
    }

    public IPromptsResource PromptsResource
    {
        get { return ServiceLocator.GetService<IPromptsResource>()!; }
    }

    #endregion

    #region tasks

    public ICommand NewTaskCommand => new ActionCommand(o => { AddTask(new ProjectTask(this)); });

    public void AddTask(ProjectTask task)
    {
        task.ResponseCompleted += TaskOnResponseCompleted;
        this.Tasks.Add(task);
    }

    public void RemoveTask(ProjectTask task)
    {
        task.ResponseCompleted -= TaskOnResponseCompleted;
        this.Tasks.Remove(task);
    }

    private void TaskOnResponseCompleted(CompletedResult obj)
    {
        this.TokensConsumption += obj.Tokens;
        this.TotalPrice += (obj.Price ?? 0);
    }

    public ObservableCollection<ProjectTask> Tasks { get; set; }

    private ProjectTask? _selectedTask;

    public ProjectTask? SelectedTask
    {
        get => _selectedTask;
        set
        {
            if (Equals(value, _selectedTask)) return;
            _selectedTask = value;
            OnPropertyChanged();
        }
    }

    #endregion

    private readonly string[] _notTrackingProperties =
    [
        nameof(EditTime),
        nameof(SelectedTask)
    ];

    private IEndpointService EndpointService => ServiceLocator.GetService<IEndpointService>()!;

    public ProjectViewModel(ILLMClient modelClient, IEnumerable<ProjectTask>? tasks = null) : base()
    {
        Requester = new RequesterViewModel(modelClient, GetResponse)
        {
            FunctionSelector =
            {
                FunctionEnabled = true,
                SelectedFunctions = this.AllowedFunctions,
            }
        };
        Requester.FunctionSelector.ConnectSource(new ProxyFunctionGroupSource(FunctionGroupsFunc));
        this.ConfigViewModel = new ProjectConfigViewModel(this.EndpointService, this);
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
        _functionSelector.ConnectDefault();
    }

    private IEnumerable<IAIFunctionGroup> FunctionGroupsFunc()
    {
        return this.AllowedFunctions;
    }

    private Task<CompletedResult> GetResponse(ILLMClient arg1, IRequestItem arg2)
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
        return SelectedTask.NewRequest(arg1, arg2);
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

        if (LanguageNames == null || LanguageNames.Count == 0)
        {
            this.AddError("LanguageNames cannot be null or empty.", nameof(LanguageNames));
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
        foreach (var aiFunctionGroup in this.Requester.FunctionSelector.SelectedFunctions)
        {
            if (aiFunctionGroup is FileSystemPlugin fileSystemPlugin)
            {
                fileSystemPlugin.AllowedPaths = AllowedFolderPaths;
            }
        }
    }

    public void NewTaskRequest()
    {
        if (this._selectedTask == null)
        {
            return;
        }
    }
}