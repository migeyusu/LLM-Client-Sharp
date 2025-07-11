using System.IO;
using LLMClient.Abstraction;
using LLMClient.UI.Component;
using Microsoft.Win32;

namespace LLMClient.UI;

public abstract class FileBasedSessionBase : NotifyDataErrorInfoViewModelBase, ILLMSession
{
    private DateTime _editTime;
    private string? _fileFullPath;

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

    /// <summary>
    /// 用于跟踪对话文件
    /// </summary>
    public string FileFullPath
    {
        get
        {
            if (_fileFullPath == null)
            {
                _fileFullPath = Path.GetFullPath($"{Guid.NewGuid()}.json", DefaultSaveFolderPath);
            }

            return _fileFullPath;
        }
        set => _fileFullPath = value;
    }

    /// <summary>
    /// indicate whether data is changed after loading.
    /// </summary>
    public abstract bool IsDataChanged { get; set; }

    public abstract bool IsBusy { get; }

    protected abstract string DefaultSaveFolderPath { get; }

    protected FileBasedSessionBase()
    {
    }


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

    protected abstract Task SaveToStream(Stream stream);

    public async Task SaveToLocal()
    {
        if (!this.IsDataChanged)
        {
            return;
        }

        var fileInfo = this.GetAssociateFile();
        if (fileInfo.Exists)
        {
            fileInfo.Delete();
        }

        await using (var fileStream = fileInfo.OpenWrite())
        {
            await SaveToStream(fileStream);
        }

        this.IsDataChanged = false;
    }
}