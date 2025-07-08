using System.IO;
using LLMClient.Abstraction;
using LLMClient.UI.Component;
using Microsoft.Win32;

namespace LLMClient.UI;

public abstract class FileBasedSessionBase : NotifyDataErrorInfoViewModelBase, ILLMSession
{
    private DateTime _editTime;

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
    /// 用于跟踪对话对象，新实例自动创建id
    /// </summary>
    public string FileName { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// indicate whether data is changed after loading.
    /// </summary>
    public abstract bool IsDataChanged { get; set; }

    public abstract bool IsBusy { get; }

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

    protected abstract string SaveFolderPath { get; }

    protected FileInfo GetAssociateFile()
    {
        var fullPath = this.SaveFolderPath;
        var fileName = Path.GetFullPath(this.FileName + ".json", fullPath);
        return new FileInfo(fileName);
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