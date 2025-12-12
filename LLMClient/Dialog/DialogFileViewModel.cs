using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using AutoMapper;
using LLMClient.Abstraction;
using LLMClient.Component.ViewModel;
using LLMClient.Configuration;
using LLMClient.Data;

namespace LLMClient.Dialog;

public class DialogFileViewModel : FileBasedSessionBase, ILLMSessionLoader<DialogFileViewModel>
{
    public DialogViewModel Dialog { get; }

    public const string SaveFolder = "Dialogs";

    public override bool IsDataChanged
    {
        get => this.Dialog.IsDataChanged;
        set => this.Dialog.IsDataChanged = value;
    }

    public override bool IsBusy => Dialog.IsBusy;

    private static readonly Lazy<string> SaveFolderPathLazy = new Lazy<string>((() => Path.GetFullPath(SaveFolder)));

    public static string SaveFolderPath => SaveFolderPathLazy.Value;

    protected override string DefaultSaveFolderPath
    {
        get { return SaveFolderPathLazy.Value; }
    }

    protected override async Task SaveToStream(Stream stream)
    {
        var dialogSession = _mapper.Map<DialogFileViewModel, DialogFilePersistModel>(this, (options => { }));
        await JsonSerializer.SerializeAsync(stream, dialogSession, SerializerOption);
    }

    public override object Clone()
    {
        var dialogSessionClone = _mapper.Map<DialogFileViewModel, DialogFilePersistModel>(this, (options => { }));
        var cloneSession =
            _mapper.Map<DialogFilePersistModel, DialogFileViewModel>(dialogSessionClone, (options => { }));
        cloneSession.IsDataChanged = true;
        return cloneSession;
    }


    public static async Task<DialogFileViewModel?> LoadFromFile(FileInfo fileInfo, IMapper mapper)
    {
        if (!fileInfo.Exists)
        {
            return null;
        }

        try
        {
            await using (var fileStream = fileInfo.OpenRead())
            {
                var dialogSession =
                    await JsonSerializer.DeserializeAsync<DialogFilePersistModel>(fileStream, SerializerOption);
                if (dialogSession == null)
                {
                    Trace.TraceError($"加载会话{fileInfo.FullName}失败：文件内容为空");
                    return null;
                }

                if (dialogSession.Version != DialogFilePersistModel.DialogPersistVersion)
                {
                    Trace.TraceError($"加载会话{fileInfo.FullName}失败：版本不匹配");
                    return null;
                }

                var viewModel =
                    mapper.Map<DialogFilePersistModel, DialogFileViewModel>(dialogSession, (options => { }));
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

    public DialogFileViewModel Fork(IDialogItem viewModel)
    {
        var of = this.Dialog.DialogItems.IndexOf(viewModel);
        var dialogSessionClone = _mapper.Map<DialogFileViewModel, DialogFilePersistModel>(this, (options => { }));
        dialogSessionClone.DialogItems = dialogSessionClone.DialogItems?.Take(of + 1).ToArray();
        var cloneSession =
            _mapper.Map<DialogFilePersistModel, DialogFileViewModel>(dialogSessionClone, (options => { }));
        cloneSession.IsDataChanged = true;
        return cloneSession;
    }

    private IMapper _mapper;

    public DialogFileViewModel(string topic, ILLMChatClient modelClient, IMapper mapper,
        GlobalOptions options, IRagSourceCollection ragSourceCollection, IList<IDialogItem>? items = null) :
        this(new DialogViewModel(topic, modelClient, mapper, options, ragSourceCollection, items), mapper)
    {
    }

    public DialogFileViewModel(DialogViewModel dialogModel, IMapper mapper)
    {
        this.Dialog = dialogModel;
        dialogModel.PropertyChanged += (sender, args) =>
        {
            switch (args.PropertyName)
            {
                case nameof(DialogViewModel.IsBusy):
                    OnPropertyChanged(nameof(IsBusy));
                    break;
            }
        };
        _mapper = mapper;
        this.Dialog.DialogItems.CollectionChanged += DialogItemsOnCollectionChanged;
    }

    private void DialogItemsOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        this.EditTime = DateTime.Now;
    }
}