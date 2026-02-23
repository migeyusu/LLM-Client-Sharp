using System.Collections.Specialized;
using System.Diagnostics;
using System.Text.Json;
using AutoMapper;
using LLMClient.Abstraction;
using LLMClient.Component.ViewModel;
using LLMClient.Configuration;
using LLMClient.Data;
using LLMClient.Dialog.Models;

namespace LLMClient.Dialog;

public class DialogFileViewModel : FileBasedSessionBase, ILLMSessionLoader<DialogFileViewModel>
{
    private readonly IViewModelFactory _factory;
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

    public override ILLMSession CloneHeader()
    {
        var chatClient = this.Dialog.Requester.DefaultClient.CloneClient();
        return _factory.CreateViewModel<DialogFileViewModel>(this.Dialog.Topic, chatClient);
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
            return viewModel;
        }
        catch (Exception e)
        {
            Trace.TraceError(e.ToString());
            return null;
        }
    }

    private IMapper _mapper;

    public DialogFileViewModel(string topic, ILLMChatClient modelClient, IMapper mapper,
        GlobalOptions options, IRagSourceCollection ragSourceCollection, IViewModelFactory factory,
        IDialogItem? rootNode = null, IDialogItem? leaf = null) :
        this(new DialogViewModel(topic, modelClient, mapper, options, factory, rootNode, leaf), mapper, factory)
    {
        _factory = factory;
    }

    public DialogFileViewModel(DialogViewModel dialogModel, IMapper mapper, IViewModelFactory factory)
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
        _factory = factory;
        dialogModel.DialogItemsObservable.CollectionChanged += DialogItemsOnCollectionChanged;
    }

    private void DialogItemsOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        this.EditTime = DateTime.Now;
    }
}