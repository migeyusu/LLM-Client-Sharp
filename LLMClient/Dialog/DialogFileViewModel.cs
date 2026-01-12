using System.Collections.Specialized;
using System.Diagnostics;
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

    public override bool IsDataChanged
    {
        get => this.Dialog.IsDataChanged;
        set => this.Dialog.IsDataChanged = value;
    }

    public override bool IsBusy => Dialog.IsBusy;

    public const string SaveFolder = "Dialogs";

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


    public static async Task<DialogFileViewModel?> LoadFromStream(Stream fileStream, IMapper mapper)
    {
        try
        {
            var dialogSession =
                await JsonSerializer.DeserializeAsync<DialogFilePersistModel>(fileStream, SerializerOption);
            if (dialogSession == null)
            {
                return null;
            }

            if (dialogSession.Version != DialogFilePersistModel.DialogPersistVersion)
            {
                return null;
            }

            var viewModel =
                mapper.Map<DialogFilePersistModel, DialogFileViewModel>(dialogSession, (options => { }));
            viewModel.IsDataChanged = false;
            return viewModel;
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
        GlobalOptions options, IRagSourceCollection ragSourceCollection, IViewModelFactory factory,
        IList<IDialogItem>? items = null) :
        this(new DialogViewModel(topic, modelClient, mapper, options, factory, items), mapper)
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