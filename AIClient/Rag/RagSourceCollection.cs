using System.Collections;
using System.Collections.ObjectModel;
using System.ComponentModel;
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
    public ObservableCollection<IRagSource> Sources { get; set; }
        = new ObservableCollection<IRagSource>();

    public bool IsRunning
    {
        get { return this.Sources.Any(source => source.Status == RagStatus.Constructing); }
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
        if (Sources.OfType<IRagFileSource>().Any(item => item.FilePath == fileName))
        {
            MessageEventBus.Publish("文件已存在，请检查！");
            return;
        }

        var extension = Path.GetExtension(fileName);
        var fileInfo = new FileInfo(openFileDialog.FileName);
        RagFileBase source;
        try
        {
            switch (extension)
            {
                case ".txt":
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
                case ".md":
                    source = new MarkdownFile(fileInfo);
                    break;
                default:
                    MessageBox.Show("不支持的文件类型，请选择文本、Markdown、JSON、CSV、PDF或Word文档。", "错误", MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return;
            }

            await source.InitializeAsync();
            source.PropertyChanged += RagFileOnPropertyChanged;
            Sources.Add(source);
            await SaveAsync();
            MessageEventBus.Publish($"已添加文件: {fileName}");
        }
        catch (Exception e)
        {
            MessageBox.Show($"添加文件失败: {e.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }));

    public async void RagFileOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is RagFileBase ragFile)
        {
            switch (e.PropertyName)
            {
                case nameof(RagFileBase.Status):
                    if (ragFile.Status == RagStatus.Constructed)
                    {
                        await this.SaveAsync();
                    }

                    break;
            }
        }
    }

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
                if (fileSource.Status == RagStatus.Constructing)
                {
                    MessageBox.Show("文件正在构建中，请停止后删除。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                    // await fileSource.StopConstruct();
                }

                await fileSource.DeleteAsync();
                fileSource.PropertyChanged -= RagFileOnPropertyChanged;
                Sources.Remove(fileSource);
                MessageEventBus.Publish($"文件{fileSource.FilePath}已删除");
                await SaveAsync();
            }
        }
        catch (Exception e)
        {
            MessageBox.Show("删除文件失败: " + e.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    });

    public int Count
    {
        get { return Sources.Count; }
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

    private static readonly SemaphoreSlim SaveSemaphore = new SemaphoreSlim(1, 1);

    public async Task SaveAsync()
    {
        var fullPath = Path.GetFullPath(ConfigFileName);
        var json = JsonSerializer.Serialize(this.Sources, Extension.DefaultJsonSerializerOptions);
        try
        {
            await SaveSemaphore.WaitAsync();
            await File.WriteAllTextAsync(fullPath, json);
        }
        finally
        {
            SaveSemaphore.Release();
        }

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
                    JsonSerializer.Deserialize<IList<IRagSource>>(fileStream,
                        Extension.DefaultJsonSerializerOptions);
                if (deserialize != null)
                {
                    this.Sources = new ObservableCollection<IRagSource>(deserialize);
                }
            }

            foreach (var ragFileSource in this.Sources)
            {
                await ragFileSource.InitializeAsync();
                if (ragFileSource is RagFileBase ragFile)
                {
                    ragFile.PropertyChanged += RagFileOnPropertyChanged;
                }
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