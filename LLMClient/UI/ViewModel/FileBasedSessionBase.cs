using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Input;
using AutoMapper;
using CommunityToolkit.Mvvm.Input;
using LLMClient.Abstraction;
using LLMClient.Data;
using LLMClient.UI.Component.Utility;
using LLMClient.UI.ViewModel.Base;
using Microsoft.Win32;

namespace LLMClient.UI.ViewModel;

public abstract class FileBasedSessionBase : NotifyDataErrorInfoViewModelBase, ILLMSession
{
    private DateTime _editTime = DateTime.Now;

    public DateTime EditTime
    {
        get => _editTime;
        set
        {
            if (value.Equals(_editTime)) return;
            _editTime = value;
            OnPropertyChanged();
        }
    }

    private string? _fileFullPath;

    /// <summary>
    /// 用于跟踪对话文件
    /// </summary>
    public string FileFullPath
    {
        get { return _fileFullPath ??= Path.GetFullPath($"{Guid.NewGuid()}.json", DefaultSaveFolderPath); }
        set => _fileFullPath = value;
    }

    /// <summary>
    /// indicate whether data is changed after loading.
    /// </summary>
    public abstract bool IsDataChanged { get; set; }

    public abstract bool IsBusy { get; }

    protected abstract string DefaultSaveFolderPath { get; }

    public static readonly JsonSerializerOptions SerializerOption = new JsonSerializerOptions()
    {
        WriteIndented = true,
        ReferenceHandler = ReferenceHandler.Preserve,
        TypeInfoResolver = LLM_DataSerializeContext.Default,
        Converters = { new AdditionalPropertiesConverter() }
    };

    protected FileBasedSessionBase()
    {
    }

    public ICommand BackupCommand => new AsyncRelayCommand(Backup);

    public virtual async Task Backup()
    {
        var saveFileDialog = new SaveFileDialog()
        {
            AddExtension = true, DefaultExt = ".json", CheckPathExists = true,
            Filter = "json files (*.json)|*.json"
        };

        if (saveFileDialog.ShowDialog() != true)
        {
            return;
        }

        var fileName = saveFileDialog.FileName;
        var fileInfo = new FileInfo(fileName);
        await using (var fileStream = fileInfo.OpenWrite())
        {
            await SaveToStream(fileStream);
        }

        MessageEventBus.Publish("已备份");
    }

    public abstract object Clone();

    public void Delete()
    {
        var associateFile = this.GetAssociateFile();
        if (associateFile.Exists)
        {
            associateFile.Delete();
        }
    }

    protected FileInfo GetAssociateFile()
    {
        return new FileInfo(FileFullPath);
    }

    public static async Task<IEnumerable<T>> LoadFromLocal<T>(IMapper mapper, string folderPath)
        where T : class, ILLMSession, ILLMSessionFactory<T>
    {
        var directoryInfo = new DirectoryInfo(folderPath);
        if (!directoryInfo.Exists)
        {
            directoryInfo.Create();
        }

        var concurrentBag = new ConcurrentBag<T>();
        await Parallel.ForEachAsync(directoryInfo.GetFiles("*.json"), (async (info, token) =>
        {
            var model = await T.LoadFromFile(info, mapper);
            if (model != null)
            {
                concurrentBag.Add(model);
            }
        }));
        return concurrentBag;
    }

    public static async IAsyncEnumerable<T> ImportFiles<T>(string targetFolderPath,
        IEnumerable<FileInfo> fileInfos, IMapper mapper)
        where T : class, ILLMSessionFactory<T>, ILLMSession
    {
        var targetDirectoryInfo = new DirectoryInfo(targetFolderPath);
        if (!targetDirectoryInfo.Exists)
        {
            targetDirectoryInfo.Create();
        }

        foreach (var fileInfo in fileInfos)
        {
            if (fileInfo.DirectoryName?.Equals(targetDirectoryInfo.FullName, StringComparison.OrdinalIgnoreCase) ==
                false)
            {
                T? session = null;
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
                        session = await T.LoadFromFile(info, mapper);
                    }
                }
                catch (Exception e)
                {
                    MessageEventBus.Publish("导入出现问题" + e.Message);
                    continue;
                }

                if (session == null) continue;
                yield return session;
            }
        }
    }

    protected abstract Task SaveToStream(Stream stream);

    public async Task SaveToLocal()
    {
        if (!this.IsDataChanged)
        {
            return;
        }

        //不要直接保存到文件流，防止保存错误导致数据完全丢失。
        await using (var memoryStream = new MemoryStream())
        {
            await SaveToStream(memoryStream);
            var fileInfo = this.GetAssociateFile();
            if (fileInfo.Exists)
            {
                fileInfo.Delete();
            }

            await using (var fileStream = fileInfo.OpenWrite())
            {
                memoryStream.Position = 0;
                await memoryStream.CopyToAsync(fileStream);
                await fileStream.FlushAsync();
            }
        }


        this.IsDataChanged = false;
    }
}