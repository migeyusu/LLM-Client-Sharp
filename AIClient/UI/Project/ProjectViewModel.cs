﻿using System.Collections.ObjectModel;
using System.Collections.Specialized;
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
using Microsoft.Extensions.DependencyInjection;

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
            _isDataChanged = value;
            if (value)
            {
                this.EditTime = DateTime.Now;
            }
        }
    }

    public override bool IsBusy { get; } = false;

    public ILLMClient Client
    {
        get => _client;
        set
        {
            if (Equals(value, _client)) return;
            _client = value;
            OnPropertyChanged();
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
                viewModel.FileName = Path.GetFileNameWithoutExtension(fileInfo.Name);
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

    private static readonly Lazy<string> SaveFolderPathLazy = new Lazy<string>((() => Path.GetFullPath(SaveDir)));
    private string? _description = string.Empty;
    private IList<string>? _languageNames;
    private string? _folderPath;
    private ProjectTask? _workingTask;
    private string? _name;
    private ILLMClient _client = NullLlmModelClient.Instance;
    private bool _isDataChanged = true;

    protected override string SaveFolderPath => SaveFolderPathLazy.Value;

    protected override async Task SaveToStream(Stream stream)
    {
        var dialogModel = Mapper.Map<ProjectViewModel, ProjectPersistModel>(this);
        await JsonSerializer.SerializeAsync(stream, dialogModel);
    }

    #endregion

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

    public ICommand SelectFolderCommand => new RelayCommand(() =>
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

            _folderPath = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Context));
        }
    }

    public virtual string Type { get; } = "Standard";

    public ObservableCollection<ProjectTask> Tasks { get; set; } = new ObservableCollection<ProjectTask>();

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
    
    private readonly string[] _notTrackingProperties =
    [
        
    ];
    
    public ProjectViewModel() : base()
    {
        this.PropertyChanged += (_, e) =>
        {
            var propertyName = e.PropertyName;
            if (_notTrackingProperties.Contains(propertyName))
            {
                return;
            }

            IsDataChanged = true;
        };
        Tasks.CollectionChanged+= TasksOnCollectionChanged;
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