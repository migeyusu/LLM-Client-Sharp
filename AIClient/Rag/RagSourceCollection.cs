using System.Collections;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using LLMClient.Abstraction;
using LLMClient.UI;
using LLMClient.UI.Component;
using Microsoft.Win32;
using Microsoft.Xaml.Behaviors.Core;

namespace LLMClient.Rag;

public class RagSourceCollection : BaseViewModel, IRagSourceCollection
{
    public ObservableCollection<IRagFileSource> FileSources { get; set; } = [];

    public bool IsRunning
    {
        get { return this.FileSources.Any(source => source.Status == ConstructStatus.Constructing); }
    }

    public ICommand AddFileCommand => new ActionCommand((async o =>
    {
        var openFileDialog = new OpenFileDialog
        {
            Filter = "All Files (*.*)|*.*",
            Multiselect = false,
            CheckFileExists = true,
        };
        if (openFileDialog.ShowDialog() != true) return;
        var fileName = openFileDialog.FileName;
        if (FileSources.Any(item => item.FilePath == fileName))
        {
            MessageEventBus.Publish("文件已存在，请检查！");
            return;
        }

        var extension = Path.GetExtension(fileName);
        var fileInfo = new FileInfo(openFileDialog.FileName);
        IRagFileSource source;
        try
        {
            switch (extension)
            {
                case ".txt":
                case ".md":
                case ".json":
                case ".csv":
                    source = new TextFile(fileInfo);
                    break;
                case ".xls":
                case ".xlsx":
                    source = new ExcelFile(fileInfo);
                    break;
                case ".pdf":
                    source = new PdfFile(fileInfo);
                    break;
                case ".doc":
                case ".docx":
                    source = new WordFile(fileInfo);
                    break;
                default:
                    MessageBox.Show("不支持的文件类型，请选择文本、Markdown、JSON、CSV、PDF或Word文档。", "错误", MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return;
            }

            FileSources.Add(source);
            await SaveAsync();
            MessageEventBus.Publish($"已添加文件: {fileName}");
        }
        catch (Exception e)
        {
            MessageBox.Show($"添加文件失败: {e.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }));

    public ICommand RemoveFileCommand => new ActionCommand(async o =>
    {
        try
        {
            if (MessageBox.Show("请确认是否删除文件？", "提示",
                    MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No,
                    MessageBoxOptions.DefaultDesktopOnly) != MessageBoxResult.Yes)
            {
                return;
            }

            if (o is RagFileBase fileSource)
            {
                if (fileSource.Status == ConstructStatus.Constructing)
                {
                    await fileSource.StopConstruct();
                }

                await fileSource.DeleteAsync();
                FileSources.Remove(fileSource);
                MessageEventBus.Publish($"文件{fileSource.FilePath}已删除");
            }
        }
        catch (Exception e)
        {
            MessageBox.Show("删除文件失败: " + e.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    });

    public IEnumerator<IRagSource> GetEnumerator()
    {
        return FileSources.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }


    public int Count
    {
        get { return FileSources.Count; }
    }

    private bool _isLoaded;

    public bool IsLoaded
    {
        get => _isLoaded;
        set
        {
            if (value == _isLoaded) return;
            _isLoaded = value;
            OnPropertyChanged();
        }
    }

    public ICommand SaveCommand => new ActionCommand(async o => { await SaveAsync(); });

    public const string ConfigFileName = "rag_file_collection.json";

    public async Task SaveAsync()
    {
        if (FileSources.DistinctBy(item => item.FilePath).Count() != FileSources.Count)
        {
            MessageEventBus.Publish("文件不能重复，请检查！");
            return;
        }

        var fullPath = Path.GetFullPath(ConfigFileName);
        var json = JsonSerializer.Serialize(this.FileSources, Extension.DefaultJsonSerializerOptions);
        await File.WriteAllTextAsync(fullPath, json);
        MessageEventBus.Publish("已保存Rag配置");
    }

    public async Task LoadAsync()
    {
        if (IsLoaded)
        {
            return;
        }

        var fileInfo = new FileInfo(Path.GetFullPath(ConfigFileName));
        if (!fileInfo.Exists)
        {
            return;
        }

        try
        {
            await using (var fileStream = fileInfo.OpenRead())
            {
                var deserialize =
                    JsonSerializer.Deserialize<IList<IRagFileSource>>(fileStream,
                        Extension.DefaultJsonSerializerOptions);
                if (deserialize != null)
                {
                    this.FileSources = new ObservableCollection<IRagFileSource>(deserialize);
                }
            }

            foreach (var ragFileSource in this.FileSources)
            {
                await ragFileSource.InitializeAsync();
            }

            this.IsLoaded = true;
            MessageEventBus.Publish("已加载Rag配置");
        }
        catch (Exception e)
        {
            Trace.TraceError(e.ToString());
        }
    }
}