using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using AutoMapper;
using LLMClient.Abstraction;
using LLMClient.Data;
using LLMClient.UI.Component;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.VectorData;

namespace LLMClient.UI.Project;

public class ProjectViewModel : FileBasedSessionBase
{
    public const string SaveDir = "Projects";

    public ProjectViewModel(ILLMClient defaultClient) : base()
    {
    }

    private static IMapper Mapper => ServiceLocator.GetService<IMapper>()!;

    public override bool IsDataChanged { get; set; }
    public override bool IsBusy { get; } = false;

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

    protected override string SaveFolderPath => SaveFolderPathLazy.Value;

    protected override async Task SaveToStream(Stream stream)
    {
        var dialogModel = Mapper.Map<ProjectViewModel, ProjectPersistModel>(this);
        await JsonSerializer.SerializeAsync(stream, dialogModel);
    }

    #endregion

    /// <summary>
    /// sample:this is a *** project
    /// </summary>
    public string? Description
    {
        get => _description;
        set
        {
            if (value == _description) return;
            _description = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Context));
            this.ClearError();
            if (string.IsNullOrEmpty(value))
            {
                this.AddError("Description cannot be null or empty.");
            }
        }
    }

    public IList<string>? LanguageNames
    {
        get => _languageNames;
        set
        {
            if (Equals(value, _languageNames)) return;
            _languageNames = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Context));
            this.ClearError();
            if (value == null)
            {
                AddError("LanguageNames cannot be null.");
            }
        }
    }

    /// <summary>
    /// 项目级别的上下文，在task间共享
    /// </summary>
    public string? Context
    {
        get
        {
            if (this.HasErrors)
            {
                return null;
            }

            if (LanguageNames == null || FolderPath == null || Description == null)
            {
                return null;
            }

            return $"我正在Windows使用语言{string.Join(",", LanguageNames)}上开发位于{FolderPath}的一个软件项目，该项目是{Description}，";
        }
    }

    public string? FolderPath
    {
        get => _folderPath;
        set
        {
            if (value == _folderPath) return;
            _folderPath = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Context));
            this.ClearError();
            if (string.IsNullOrEmpty(value))
            {
                this.AddError("FolderPath cannot be null or empty.");
                return;
            }

            if (!Directory.Exists(value))
            {
                try
                {
                    Directory.CreateDirectory(value);
                }
                catch (Exception e)
                {
                    var error = $"创建文件夹 {value} 失败: {e.Message}";
                    Trace.TraceError(error);
                    this.AddError(error);
                }
            }
        }
    }

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

    public void NewTaskRequest()
    {
        if (this._workingTask == null)
        {
            return;
        }
        
    }
}