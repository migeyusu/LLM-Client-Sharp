﻿using System.Collections.ObjectModel;
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

    public override bool IsDataChanged { get; set; } = true;

    public override bool IsBusy { get; } = false;

    private static readonly Lazy<string> SaveFolderPathLazy = new Lazy<string>((() => Path.GetFullPath(SaveDir)));

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

            Debug.Assert(LanguageNames != null, nameof(LanguageNames) + " != null");
            return $"我正在Windows使用语言{string.Join(",", LanguageNames)}上开发位于{FolderPath}的一个软件项目，该项目是{Description}，";
        }
    }

    private AIFunctionSelectorViewModel _functionSelector = new((() =>
    {
        DialogHost.CloseDialogCommand.Execute(true, null);
    }));

    public ICommand SelectFunctionsCommand => new RelayCommand(async () =>
    {
        _functionSelector.ResetSource()
            .EnsureAsync();
        var selectedFunctions = this.AllowedFunctions;
        if (selectedFunctions != null)
        {
            _functionSelector.SelectedFunctions = selectedFunctions;
        }

        if ((await DialogHost.Show(_functionSelector)) is true)
        {
            this.AllowedFunctions = _functionSelector.SelectedFunctions;
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

    #endregion

    #region tasks

    public ICommand NewTaskCommand => new ActionCommand(o => { AddTask(new ProjectTask(this)); });

    public void AddTask(ProjectTask task)
    {
        task.PropertyChanged += TaskOnPropertyChanged;
        task.DialogItems.CollectionChanged += OnCollectionChanged;
        this.Tasks.Add(task);
    }

    public void RemoveTask(ProjectTask task)
    {
        if (this.Tasks.Remove(task))
        {
            task.DialogItems.CollectionChanged -= OnCollectionChanged;
            task.PropertyChanged -= TaskOnPropertyChanged;
        }
    }

    private void TaskOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        this.IsDataChanged = true;
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

    private long _tokensConsumption;
    private float _totalPrice;
    private IAIFunctionGroup[]? _allowedFunctions;

    private IEndpointService EndpointService => ServiceLocator.GetService<IEndpointService>()!;

    public ProjectViewModel(ILLMClient modelClient, IEnumerable<ProjectTask>? tasks = null) : base()
    {
        Requester = new RequesterViewModel(modelClient, GetResponse);
        Requester.FunctionSelector.ConnectSource(new ProxyFunctionGroupSource(FunctionGroupsFunc));
        this.ConfigViewModel = new ProjectConfigViewModel(this.EndpointService, this);
        this.Tasks = new ObservableCollection<ProjectTask>();
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
        return this.AllowedFunctions ?? Enumerable.Empty<IAIFunctionGroup>();
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

        if (!this.Validate())
        {
            return Task.FromException<CompletedResult>(new InvalidOperationException("当前项目配置不合法"));
        }

        return SelectedTask.SendRequestCore(arg1, arg2);
    }

    public virtual void Initialize()
    {
        AllowedFunctions = [new FileSystemPlugin(), new WinCLIPlugin()];
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
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
        if (this._selectedTask == null)
        {
            return;
        }
    }
}