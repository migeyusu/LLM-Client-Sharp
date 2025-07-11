using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows.Input;
using AutoMapper;
using CommunityToolkit.Mvvm.Input;
using LLMClient.Abstraction;
using LLMClient.Data;
using LLMClient.Endpoints;
using LLMClient.UI.Component;
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

    public override bool IsDataChanged
    {
        get => _isDataChanged;
        set
        {
            if (_isDataChanged == value)
                return;
            _isDataChanged = value;
            if (value)
            {
                this.EditTime = DateTime.Now;
            }
        }
    }

    public override bool IsBusy { get; } = false;

    private static readonly Lazy<string> SaveFolderPathLazy = new Lazy<string>((() => Path.GetFullPath(SaveDir)));

    protected override string DefaultSaveFolderPath
    {
        get { return SaveFolderPathLazy.Value; }
    }

    public ILLMClient Client
    {
        get => _client;
        set
        {
            if (Equals(value, _client)) return;
            if (_client is INotifyPropertyChanged oldValue)
            {
                oldValue.PropertyChanged -= TagDataChangedOnPropertyChanged;
            }

            if (_client.Parameters is INotifyPropertyChanged oldParameters)
            {
                oldParameters.PropertyChanged -= TagDataChangedOnPropertyChanged;
            }

            _client = value;
            OnPropertyChanged();
            TrackClientChanged(value);
        }
    }

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
                var persistModel = await JsonSerializer.DeserializeAsync<ProjectPersistModel>(fileStream);
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

                var viewModel = Mapper.Map<ProjectPersistModel, ProjectViewModel>(persistModel);
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
    private ProjectTask? _workingTask;
    private string? _name;
    private ILLMClient _client = NullLlmModelClient.Instance;
    private bool _isDataChanged = true;

    protected override async Task SaveToStream(Stream stream)
    {
        var dialogModel = Mapper.Map<ProjectViewModel, ProjectPersistModel>(this);
        await JsonSerializer.SerializeAsync(stream, dialogModel);
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


    /// <summary>
    /// 项目级别的上下文，在task间共享
    /// </summary>
    public string? Context
    {
        get
        {
            if (!Validate())
            {
                return null;
            }

            return $"我正在Windows使用语言{string.Join(",", LanguageNames)}上开发位于{FolderPath}的一个软件项目，该项目是{Description}，";
        }
    }

    public ICommand SelectFunctionsCommand => new RelayCommand(async () =>
    {
        var functionSelection = new AIFunctionSelectionViewModel(this.AllowedFunctions ??
                                                                 Enumerable.Empty<IAIFunctionGroup>(), true);
        functionSelection.EnsureAsync();
        await DialogHost.Show(functionSelection);
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

    #endregion

    #region tasks

    public ICommand AddNewTask => new ActionCommand(o => { this.Tasks.Add(new ProjectTask()); });

    public void DeleteTask(ProjectTask task)
    {
        this.Tasks.Remove(task);
    }

    public ObservableCollection<ProjectTask> Tasks { get; set; }

    public ProjectTask? WorkingTask
    {
        get => _workingTask;
        set
        {
            if (Equals(value, _workingTask)) return;
            _workingTask = value;
            OnPropertyChanged();
        }
    }

    #endregion

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

    public float TotalPrice
    {
        get => _totalPrice;
        set
        {
            if (value.Equals(_totalPrice)) return;
            _totalPrice = value;
            OnPropertyChanged();
        }
    }

    public IAIFunctionGroup[]? AllowedFunctions
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

    private readonly string[] _notTrackingProperties =
    [
    ];

    private long _tokensConsumption;
    private float _totalPrice;
    private IAIFunctionGroup[]? _allowedFunctions;

    public ProjectViewModel(IEnumerable<ProjectTask>? tasks = null) : base()
    {
        this.Tasks = tasks == null
            ? []
            : new ObservableCollection<ProjectTask>(tasks);
        this.PropertyChanged += (_, e) =>
        {
            var propertyName = e.PropertyName;
            if (_notTrackingProperties.Contains(propertyName))
            {
                return;
            }

            IsDataChanged = true;
        };
        Tasks.CollectionChanged += TasksOnCollectionChanged;
    }

    public virtual void Initialize()
    {
        AllowedFunctions = [new FileSystemPlugin(), new WinCLIPlugin()];
    }

    private void TasksOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        this.IsDataChanged = true;
    }

    public bool Validate()
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

        return !this.HasErrors;
    }

    public void NewTaskRequest()
    {
        if (this._workingTask == null)
        {
            return;
        }
    }
}