using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using AutoMapper;
using LLMClient.Abstraction;
using LLMClient.Data;
using LLMClient.UI.Component;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;

namespace LLMClient.UI.Dialog;

public class DialogSession : FileBasedSessionBase
{
    public DialogViewModel Dialog { get; }

    public const string SaveFolder = "Dialogs";

    public static readonly Lazy<string> SaveFolderPathLazy = new Lazy<string>((() => Path.GetFullPath(SaveFolder)));

    public override bool IsDataChanged
    {
        get => this.Dialog.IsDataChanged;
        set => this.Dialog.IsDataChanged = value;
    }

    public override bool IsBusy => Dialog.IsBusy;

    protected override string SaveFolderPath => SaveFolderPathLazy.Value;

    protected override async Task SaveToStream(Stream stream)
    {
        var dialogSession = Mapper.Map<DialogSession, DialogSessionPersistModel>(this);
        await JsonSerializer.SerializeAsync(stream, dialogSession);
    }

    public static async IAsyncEnumerable<DialogSession> LoadFromLocal()
    {
        string fullPath = SaveFolderPathLazy.Value;
        var directoryInfo = new DirectoryInfo(fullPath);
        if (!directoryInfo.Exists)
        {
            directoryInfo.Create();
        }

        foreach (var fileInfo in directoryInfo.GetFiles())
        {
            var dialogSession = await LoadFromFile(fileInfo);
            if (dialogSession == null)
            {
                continue;
            }

            yield return dialogSession;
        }
    }

    public static async Task<DialogSession?> LoadFromFile(FileInfo fileInfo,
        int version = DialogSessionPersistModel.DialogPersistVersion)
    {
        if (!fileInfo.Exists)
        {
            return null;
        }

        try
        {
            await using (var fileStream = fileInfo.OpenRead())
            {
                var dialogModel = await JsonSerializer.DeserializeAsync<DialogSessionPersistModel>(fileStream);
                if (dialogModel == null)
                {
                    Trace.TraceError($"加载会话{fileInfo.FullName}失败：文件内容为空");
                    return null;
                }

                if (dialogModel.Version != version)
                {
                    Trace.TraceError($"加载会话{fileInfo.FullName}失败：版本不匹配");
                    return null;
                }

                var viewModel = Mapper.Map<DialogSessionPersistModel, DialogSession>(dialogModel);
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

    public async Task<DialogSession> Fork(IDialogItem viewModel)
    {
        var of = this.Dialog.DialogItems.IndexOf(viewModel);
        var dialogSessionClone = Mapper.Map<DialogSession, DialogSessionPersistModel>(this);
        dialogSessionClone.DialogItems = dialogSessionClone.DialogItems?.Take(of + 1).ToArray();
        var fileInfo = new FileInfo(Path.Combine(SaveFolderPath, $"{Guid.NewGuid()}.json"));
        await using (var fileStream = fileInfo.OpenWrite())
        {
            await JsonSerializer.SerializeAsync(fileStream, dialogSessionClone);
        }

        var dialogSession = Mapper.Map<DialogSessionPersistModel, DialogSession>(dialogSessionClone);
        dialogSession.FileName = Path.GetFileNameWithoutExtension(fileInfo.Name);
        dialogSession.IsDataChanged = false;
        return dialogSession;
    }

    public static async IAsyncEnumerable<DialogSession> ImportFiles(IEnumerable<FileInfo> fileInfos)
    {
        var targetFolderPath = SaveFolderPathLazy.Value;
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
                DialogSession? dialogSession = null;
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
                        dialogSession = await LoadFromFile(info);
                    }
                }
                catch (Exception e)
                {
                    MessageEventBus.Publish("导入出现问题" + e.Message);
                    continue;
                }

                if (dialogSession == null) continue;
                yield return dialogSession;
            }
        }
    }

    private static IMapper Mapper => ServiceLocator.GetService<IMapper>()!;

    public DialogSession(string topic, ILLMClient modelClient,
        IList<IDialogItem>? items = null) :
        this(new DialogViewModel(topic, modelClient, items))
    {
    }

    public DialogSession(DialogViewModel dialogModel)
    {
        this.Dialog = dialogModel;
        this.Dialog.DialogItems.CollectionChanged += DialogItemsOnCollectionChanged;
    }

    private void DialogItemsOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        this.EditTime = DateTime.Now;
    }
}