using System.Collections.ObjectModel;
using System.Diagnostics;
using AutoMapper;
using LLMClient.Abstraction;
using LLMClient.Component.ViewModel;
using LLMClient.Configuration;

using LLMClient.Dialog;
using LLMClient.Dialog.Models;
using LLMClient.Endpoints;

using LLMClient.Persistence;
using LLMClient.Project;

namespace LLMClient;

public class SessionProjectPersistenceProfile : Profile
{
    private readonly IViewModelFactory _viewModelFactory;

    public SessionProjectPersistenceProfile(IViewModelFactory viewModelFactory, IPromptsResource promptsResource)
    {
        _viewModelFactory = viewModelFactory;
        const string parentDialogViewModelKey = AutoMapModelTypeConverter.ParentDialogViewModelKey;

        CreateMap<ProjectOptionsPersistModel, ProjectOption>();
        CreateMap<ProjectOption, ProjectOptionsPersistModel>();

        CreateMap<ProjectPersistModel, ProjectViewModel>()
            .Include<GeneralProjectPersistModel, GeneralProjectViewModel>()
            .Include<CSharpProjectPersistModel, CSharpProjectViewModel>()
            .Include<CppProjectPersistModel, CppProjectViewModel>()
            .ForMember(dest => dest.Session, opt => opt.Ignore())
            .ForMember(dest => dest.ExtendedSystemPrompts,
                opt =>
                {
                    opt.PreCondition(src => src.ExtendedPrompts != null);
                    opt.MapFrom(src => MapPrompts(src.ExtendedPrompts, promptsResource));
                })
            .AfterMap(AfterMapProjectViewModel);

        CreateMap<GeneralProjectPersistModel, GeneralProjectViewModel>()
            .IncludeBase<ProjectPersistModel, ProjectViewModel>()
            .ConstructUsing(CreateViewModel<GeneralProjectViewModel>);
        CreateMap<CSharpProjectPersistModel, CSharpProjectViewModel>()
            .IncludeBase<ProjectPersistModel, ProjectViewModel>()
            .ConstructUsing(CreateViewModel<CSharpProjectViewModel>)
            .ForMember(dest => dest.SolutionFilePath, opt => opt.MapFrom(src => src.SolutionFilePath));
        CreateMap<CppProjectPersistModel, CppProjectViewModel>()
            .IncludeBase<ProjectPersistModel, ProjectViewModel>()
            .ConstructUsing(CreateViewModel<CppProjectViewModel>);

        CreateMap<ProjectViewModel, ProjectPersistModel>()
            .Include<GeneralProjectViewModel, GeneralProjectPersistModel>()
            .Include<CSharpProjectViewModel, CSharpProjectPersistModel>()
            .Include<CppProjectViewModel, CppProjectPersistModel>()
            .ForMember(dest => dest.Client, opt => opt.MapFrom(src => src.Requester.DefaultClient))
            .ForMember(dest => dest.Sessions, opt => opt.MapFrom(src => src.Session))
            .ForMember(dest => dest.ExtendedPrompts,
                opt =>
                {
                    opt.PreCondition(src => src.ExtendedSystemPrompts.Any());
                    opt.MapFrom(src => new PromptsPersistModel
                    {
                        PromptReference = src.ExtendedSystemPrompts.Select(entry => entry.Id).ToArray(),
                    });
                })
            .ForMember(dest => dest.UserPrompt, opt => opt.MapFrom(src => src.Requester.PromptEditViewModel.FinalText));
        CreateMap<GeneralProjectViewModel, GeneralProjectPersistModel>()
            .IncludeBase<ProjectViewModel, ProjectPersistModel>();
        CreateMap<CSharpProjectViewModel, CSharpProjectPersistModel>()
            .IncludeBase<ProjectViewModel, ProjectPersistModel>()
            .ForMember(dest => dest.SolutionFilePath, opt => opt.MapFrom(src => src.SolutionFilePath));
        CreateMap<CppProjectViewModel, CppProjectPersistModel>()
            .IncludeBase<ProjectViewModel, ProjectPersistModel>();

        CreateMap<DialogSessionViewModel, DialogSessionPersistModel>()
            .Include<DialogViewModel, DialogFilePersistModel>()
            .Include<ProjectSessionViewModel, ProjectSessionPersistModel>()
            .ForMember(model => model.AllowedFunctions,
                expression => expression.MapFrom(model => model.SelectedFunctionGroups))
            .ForMember(model => model.DialogItems, opt => opt.Ignore())
            .ForMember(model => model.RootNode, expression => expression.Ignore())
            .ForMember(model => model.CurrentLeaf, expression => expression.Ignore())
            .AfterMap((source, destination, context) =>
                FlattenTreeForSave(source, destination, context.Mapper));

        CreateMap<DialogViewModel, DialogFilePersistModel>()
            .IncludeBase<DialogSessionViewModel, DialogSessionPersistModel>()
            .ForMember(model => model.ExtendedPrompts, expression =>
            {
                expression.PreCondition(src => src.ExtendedSystemPrompts.Any());
                expression.MapFrom(s => new PromptsPersistModel
                {
                    PromptReference = s.ExtendedSystemPrompts.Select(entry => entry.Id).ToArray(),
                });
            })
            .ForMember(model => model.IsFunctionEnabled,
                expression => expression.MapFrom(model => model.Requester.FunctionTreeSelector.IsFunctionEnabled))
            .ForMember(model => model.PromptString,
                opt => opt.MapFrom(src => src.Requester.PromptEditViewModel.FinalText))
            .ForMember(model => model.Client, opt => opt.MapFrom(src => src.Requester.DefaultClient))
            .ForMember(model => model.AgentOption, opt => opt.MapFrom(src => src.Requester.AgentOption));

        CreateMap<ProjectSessionViewModel, ProjectSessionPersistModel>()
            .IncludeBase<DialogSessionViewModel, DialogSessionPersistModel>()
            .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Topic));

        CreateMap<DialogSessionPersistModel, DialogSessionViewModel>()
            .Include<DialogFilePersistModel, DialogViewModel>()
            .Include<ProjectSessionPersistModel, ProjectSessionViewModel>()
            .ForMember(dest => dest.DialogItems, opt => opt.Ignore())
            .ForMember(dest => dest.RootNode, opt => opt.Ignore())
            .ForMember(dest => dest.CurrentLeaf, opt => opt.Ignore())
            .ForMember(dest => dest.SelectedFunctionGroups, opt => opt.MapFrom(src => src.AllowedFunctions))
            .AfterMap((src, dest, ctx) =>
            {
                try
                {
                    ctx.Items[parentDialogViewModelKey] = dest;
                    BuildTreeFromFlatList(ctx.Mapper, src, dest);
                }
                finally
                {
                    ctx.Items.Remove(parentDialogViewModelKey);
                }
            });

        CreateMap<DialogFilePersistModel, DialogViewModel>()
            .IncludeBase<DialogSessionPersistModel, DialogSessionViewModel>()
            .ConstructUsing((model, context) =>
            {
                var llmClient = model.Client == null
                    ? EmptyLlmModelClient.Instance
                    : context.Mapper.Map<ParameterizedLLMModelPO, ILLMChatClient>(model.Client);
                var modelPromptString = model.PromptString ?? string.Empty;
                return _viewModelFactory.CreateViewModel<DialogViewModel>(model.Topic, modelPromptString, llmClient);
            })
            .ForMember(dest => dest.ExtendedSystemPrompts,
                opt =>
                {
                    opt.PreCondition(src => src.ExtendedPrompts != null);
                    opt.MapFrom(src => MapPrompts(src.ExtendedPrompts, promptsResource));
                })
            .ForPath(model => model.Requester.AgentOption,
                opt => { opt.MapFrom(src => src.AgentOption ?? new AgentOption()); });

        CreateMap<ProjectSessionPersistModel, ProjectSessionViewModel>()
            .IncludeBase<DialogSessionPersistModel, DialogSessionViewModel>()
            .ForMember(dest => dest.Topic, opt => opt.MapFrom(src => src.Name))
            .ConstructUsing((_, ctx) =>
            {
                if (!ctx.Items.TryGetValue(AutoMapModelTypeConverter.ParentProjectViewModelKey,
                        out var parentProjectViewModel)
                    || parentProjectViewModel is not ProjectViewModel projectViewModel)
                {
                    throw new InvalidOperationException("Parent ProjectViewModel is not set in context.");
                }

                return viewModelFactory.CreateViewModel<ProjectSessionViewModel>(projectViewModel);
            });
    }

    private TViewModel CreateViewModel<TViewModel>(ProjectPersistModel source, ResolutionContext context)
        where TViewModel : ProjectViewModel
    {
        if (source.Option == null)
        {
            throw new NotSupportedException("ProjectOptionsPersistModel 不能为空.");
        }

        var projectOption = context.Mapper.Map<ProjectOption>(source.Option);
        var client = source.Client == null
            ? EmptyLlmModelClient.Instance
            : context.Mapper.Map<ILLMChatClient>(source.Client);
        return _viewModelFactory.CreateViewModel<TViewModel>(projectOption, source.UserPrompt ?? string.Empty, client);
    }

    private static void AfterMapProjectViewModel(ProjectPersistModel source, ProjectViewModel dest,
        ResolutionContext context)
    {
        context.Items[AutoMapModelTypeConverter.ParentProjectViewModelKey] = dest;

        try
        {
            if (source.Sessions != null && source.Sessions.Length != 0)
            {
                dest.Session.Clear();
                var sessionViewModels = context.Mapper.Map<IEnumerable<ProjectSessionViewModel>>(source.Sessions);
                foreach (var sessionViewModel in sessionViewModels)
                {
                    dest.AddSession(sessionViewModel);
                }
            }
        }
        finally
        {
            context.Items.Remove(AutoMapModelTypeConverter.ParentProjectViewModelKey);
        }
    }

    private static ObservableCollection<PromptEntry>? MapPrompts(PromptsPersistModel? sourceExtendedPrompts,
        IPromptsResource promptsResource)
    {
        if (sourceExtendedPrompts != null)
        {
            var promptReference = sourceExtendedPrompts.PromptReference;
            var systemPrompts = promptsResource.SystemPrompts;
            if (promptReference != null && systemPrompts.Any())
            {
                var promptEntries = promptReference
                    .Select(id => systemPrompts.FirstOrDefault(p => p.Id == id))
                    .Where(p => p != null)
                    .OfType<PromptEntry>()
                    .ToArray();
                return new ObservableCollection<PromptEntry>(promptEntries);
            }
        }

        return null;
    }

    private static void BuildTreeFromFlatList(IMapperBase mapper, DialogSessionPersistModel source,
        IDialogGraphViewModel destination)
    {
        List<IDialogItem> vmList = [];
        var flatPersistItems = ExpandLegacySummaryItems(source.DialogItems);
        if (flatPersistItems == null || flatPersistItems.Length == 0) return;

        var idMap = new Dictionary<Guid, IDialogItem>();
        IDialogItem? lastImplicitParent = null;
        for (var index = 0; index < flatPersistItems.Length; index++)
        {
            var persistItem = flatPersistItems[index];
            if (persistItem.Id == Guid.Empty)
            {
                persistItem.Id = Guid.NewGuid();
            }

            var dialogItem = mapper.Map<IDialogPersistItem, IDialogItem>(persistItem);
            var previousItemId = persistItem.PreviousItemId ?? lastImplicitParent?.Id;

            if (previousItemId.HasValue && idMap.TryGetValue(previousItemId.Value, out var previousNode))
            {
                previousNode.AppendChild(dialogItem);
            }
            else
            {
                Trace.TraceInformation(
                    "DialogMappingProfile.BuildTreeFromFlatList: Node {0} has no valid previous node, treat as root.",
                    dialogItem.Id);
            }

            idMap[dialogItem.Id] = dialogItem;
            lastImplicitParent = dialogItem;
            vmList.Add(dialogItem);
        }

        var root = vmList.FirstOrDefault(x => x.PreviousItem == null);
        if (root != null)
        {
            destination.RootNode.AppendChild(root);
            var currentLeaf = source.CurrentLeaf;
            if (currentLeaf != null)
            {
                var dialogItem = vmList.FirstOrDefault(item => item.Id == currentLeaf.Id);
                if (dialogItem != null)
                {
                    destination.CurrentLeaf = dialogItem;
                    return;
                }
            }

            destination.CurrentLeaf = root.DefaultLastItem();
        }
    }

    private static IDialogPersistItem[]? ExpandLegacySummaryItems(IDialogPersistItem[]? dialogItems)
    {
        if (dialogItems == null || dialogItems.Length == 0)
        {
            return dialogItems;
        }

        var expandedItems = new List<IDialogPersistItem>(dialogItems.Length);
        foreach (var dialogItem in dialogItems)
        {
            if (dialogItem is not SummaryRequestPersistItem summaryRequest)
            {
                expandedItems.Add(dialogItem);
                continue;
            }

            var eraseId = Guid.NewGuid();
            expandedItems.Add(new ErasePersistItem
            {
                Id = eraseId,
                PreviousItemId = summaryRequest.PreviousItemId,
            });
            expandedItems.Add(new RequestPersistItem
            {
                Id = summaryRequest.Id,
                PreviousItemId = eraseId,
                InteractionId = summaryRequest.InteractionId,
                RawTextMessage = summaryRequest.SummaryPrompt ?? string.Empty,
                CallEngineType = FunctionCallEngineType.Prompt,
                Tokens = (long)((summaryRequest.SummaryPrompt?.Length ?? 0) / 2.8),
            });
        }

        return expandedItems.ToArray();
    }

    private static void FlattenTreeForSave(IDialogGraphViewModel source, DialogSessionPersistModel destination,
        IMapperBase mapper)
    {
        var flatList = new List<IDialogPersistItem>();
        var visited = new HashSet<Guid>();
        var rootNode = source.RootNode.Children.FirstOrDefault();
        if (rootNode == null)
        {
            return;
        }

        Visit(rootNode);
        destination.DialogItems = flatList.ToArray();
        destination.RootNode = flatList.FirstOrDefault(item => item.Id == rootNode.Id);
        var currentLeaf = source.CurrentLeaf;
        destination.CurrentLeaf = flatList.FirstOrDefault(item => item.Id == currentLeaf.Id);

        void Visit(IDialogItem item)
        {
            if (!visited.Add(item.Id)) return;
            var persistItem = mapper.Map<IDialogPersistItem>(item);
            persistItem.PreviousItemId = item.PreviousItem?.Id;
            persistItem.Id = item.Id;
            flatList.Add(persistItem);
            foreach (var child in item.Children)
            {
                Visit(child);
            }
        }
    }
}