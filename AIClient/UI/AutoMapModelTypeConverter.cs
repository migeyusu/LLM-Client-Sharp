using System.Collections.ObjectModel;
using AutoMapper;
using LLMClient.Abstraction;
using LLMClient.Data;
using LLMClient.Dialog;
using LLMClient.Endpoints;
using LLMClient.MCP;
using LLMClient.Project;

namespace LLMClient.UI;

public class AutoMapModelTypeConverter : ITypeConverter<DialogFileViewModel, DialogFilePersistModel>,
    ITypeConverter<DialogFilePersistModel, DialogFileViewModel>,
    ITypeConverter<DialogViewModel, DialogFilePersistModel>,
    ITypeConverter<DialogFilePersistModel, DialogViewModel>,
    ITypeConverter<MultiResponsePersistItem, MultiResponseViewItem>,
    ITypeConverter<MultiResponseViewItem, MultiResponsePersistItem>,
    ITypeConverter<ProjectPersistModel, ProjectViewModel>,
    ITypeConverter<ProjectViewModel, ProjectPersistModel>,
    ITypeConverter<ProjectTaskPersistModel, ProjectTaskViewModel>,
    ITypeConverter<ProjectTaskViewModel, ProjectTaskPersistModel>,
    ITypeConverter<ILLMChatClient, LLMClientPersistModel>,
    ITypeConverter<LLMClientPersistModel, ILLMChatClient>,
    ITypeConverter<CheckableFunctionGroupTree, AIFunctionGroupPersistObject>,
    ITypeConverter<AIFunctionGroupPersistObject, CheckableFunctionGroupTree>
{
    private readonly IEndpointService _endpointService;

    private readonly IMcpServiceCollection _mcpServiceCollection;

    IViewModelFactory _viewModelFactory;

    public AutoMapModelTypeConverter(IEndpointService service, IMcpServiceCollection mcpServiceCollection,
        IViewModelFactory factory)
    {
        this._endpointService = service;
        this._mcpServiceCollection = mcpServiceCollection;
        this._viewModelFactory = factory;
    }

    public DialogFilePersistModel Convert(DialogFileViewModel from, DialogFilePersistModel? destination,
        ResolutionContext context)
    {
        destination ??= new DialogFilePersistModel();
        var source = from.Dialog;
        context.Mapper.Map<DialogViewModel, DialogSessionPersistModel>(source, destination);
        destination.EditTime = from.EditTime;
        destination.Topic = source.Topic;
        return destination;
    }

    public DialogFileViewModel Convert(DialogFilePersistModel source, DialogFileViewModel? destination,
        ResolutionContext context)
    {
        var dialogViewModel = context.Mapper.Map<DialogSessionPersistModel, DialogViewModel>(source);
        if (destination != null)
        {
            throw new NotSupportedException("Cannot set dialogViewModel to exist DialogSession");
        }

        return new DialogFileViewModel(dialogViewModel)
        {
            EditTime = source.EditTime,
        };
    }

    public MultiResponseViewItem Convert(MultiResponsePersistItem source, MultiResponseViewItem? destination,
        ResolutionContext context)
    {
        var contextItems = context.Items;
        if (!contextItems.TryGetValue(ParentSessionViewModelKey, out var parentDialogViewModel)
            || !(parentDialogViewModel is DialogSessionViewModel parentViewModel))
        {
            throw new InvalidOperationException("Parent DialogViewModel is not set in context.");
        }

        var items = source.ResponseItems.Select(x =>
            context.Mapper.Map<ResponsePersistItem, ResponseViewItem>(x));
        if (destination != null)
        {
            destination.Items = new ObservableCollection<IResponseViewItem>(items);
            destination.AcceptedIndex = source.AcceptedIndex;
            destination.InteractionId = source.InteractionId;
            return destination;
        }

        return new MultiResponseViewItem(items, parentViewModel)
        {
            AcceptedIndex = source.AcceptedIndex,
            InteractionId = source.InteractionId,
        };
    }

    public MultiResponsePersistItem Convert(MultiResponseViewItem source, MultiResponsePersistItem? destination,
        ResolutionContext context)
    {
        destination ??= new MultiResponsePersistItem();
        destination.AcceptedIndex = source.AcceptedIndex;
        destination.ResponseItems = source.Items.OfType<ResponseViewItem>()
            .Select(x => context.Mapper.Map<ResponseViewItem, ResponsePersistItem>(x))
            .ToArray();
        destination.InteractionId = source.InteractionId;
        return destination;
    }

    public DialogFilePersistModel Convert(DialogViewModel source, DialogFilePersistModel? destination,
        ResolutionContext context)
    {
        if (destination == null)
        {
            destination = new DialogFilePersistModel();
        }

        var mapper = context.Mapper;
        var dialogItems = source.DialogItems.Select<IDialogItem, IDialogPersistItem>(item =>
        {
            if (item is EraseViewItem eraseViewItem)
            {
                return eraseViewItem;
            }

            if (item is RequestViewItem requestViewItem)
            {
                return mapper.Map<RequestViewItem, RequestPersistItem>(requestViewItem);
            }

            if (item is MultiResponseViewItem multiResponseViewItem)
            {
                return mapper.Map<MultiResponseViewItem, MultiResponsePersistItem>(multiResponseViewItem);
            }

            throw new NotSupportedException();
        }).ToArray();
        destination.DialogItems = dialogItems;
        destination.TokensConsumption = source.TokensConsumption;
        destination.TotalPrice = source.TotalPrice;
        destination.SystemPrompt = source.SystemPrompt;
        var requester = source.Requester;
        destination.PromptString = requester.PromptString;
        destination.AllowedFunctions = source.SelectedFunctionGroups
            ?.Select((tree => mapper.Map<CheckableFunctionGroupTree, AIFunctionGroupPersistObject>(tree))).ToArray();
        destination.Client = mapper.Map<ILLMChatClient, LLMClientPersistModel>(requester.DefaultClient);
        return destination;
    }

    private const string ParentSessionViewModelKey = "ParentDialogViewModel";

    private const string ParentProjectViewModelKey = "ParentProjectViewModel";

    public DialogViewModel Convert(DialogFilePersistModel source, DialogViewModel? destination,
        ResolutionContext context)
    {
        var mapper = context.Mapper;
        var llmClient = source.Client == null
            ? NullLlmModelClient.Instance
            : mapper.Map<LLMClientPersistModel, ILLMChatClient>(source.Client);
        if (destination != null)
        {
            destination.Topic = source.Topic;
            destination.Requester.DefaultClient = llmClient;
            destination.DialogItems.Clear();
        }
        else
        {
            destination =  _viewModelFactory.CreateViewModel<DialogViewModel>(source.Topic, llmClient);
        }

        context.Items.Add(ParentSessionViewModelKey, destination);
        try
        {
            var sourceDialogItems = source.DialogItems?.Select<IDialogPersistItem, IDialogItem>((item =>
            {
                return item switch
                {
                    MultiResponsePersistItem multiResponsePersistItem => mapper
                        .Map<MultiResponsePersistItem, MultiResponseViewItem>(multiResponsePersistItem),
                    RequestPersistItem requestPersistItem => mapper
                        .Map<RequestPersistItem, RequestViewItem>(requestPersistItem),
                    _ => (IDialogItem)item
                };
            })).ToArray();
            if (sourceDialogItems != null)
            {
                foreach (var sourceDialogItem in sourceDialogItems)
                {
                    destination.DialogItems.Add(sourceDialogItem);
                }
            }

            destination.TokensConsumption = source.TokensConsumption;
            destination.TotalPrice = source.TotalPrice;
            destination.SystemPrompt = source.SystemPrompt;
            var requester = destination.Requester;
            requester.PromptString = source.PromptString;
            destination.SelectedFunctionGroups = source.AllowedFunctions?.Select((o =>
                mapper.Map<AIFunctionGroupPersistObject, CheckableFunctionGroupTree>(o))).ToArray();
        }
        finally
        {
            context.Items.Remove(ParentSessionViewModelKey);
        }

        return destination;
    }

    public ProjectViewModel Convert(ProjectPersistModel source, ProjectViewModel? destination,
        ResolutionContext context)
    {
        var mapper = context.Mapper;
        var defaultClient = source.Client == null
            ? NullLlmModelClient.Instance
            : context.Mapper.Map<LLMClientPersistModel, ILLMChatClient>(source.Client);
        if (destination != null)
        {
            destination.Requester.DefaultClient = defaultClient;
        }
        else
        {
            destination = new ProjectViewModel(defaultClient);
        }

        context.Items.Add(ParentProjectViewModelKey, destination);
        try
        {
            destination.Tasks.Clear();
            var tasks = source.Tasks?
                .Select(task => mapper.Map<ProjectTaskPersistModel, ProjectTaskViewModel>(task))
                .ToArray();
            if (tasks != null)
            {
                foreach (var projectTask in tasks)
                {
                    destination.AddTask(projectTask);
                }
            }

            destination.Name = source.Name;
            destination.Description = source.Description;
            destination.LanguageNames = source.LanguageNames;
            destination.FolderPath = source.FolderPath;
            destination.AllowedFolderPaths = source.AllowedFolderPaths == null
                ? []
                : new ObservableCollection<string>(source.AllowedFolderPaths);
            destination.TokensConsumption = source.TokensConsumption;
            destination.TotalPrice = source.TotalPrice;
            destination.EditTime = source.EditTime;
        }
        finally
        {
            context.Items.Remove(ParentProjectViewModelKey);
        }

        return destination;
    }

    public ProjectPersistModel Convert(ProjectViewModel source, ProjectPersistModel? destination,
        ResolutionContext context)
    {
        var mapper = context.Mapper;
        var projectTaskPersistModels = source.Tasks
            ?.Select(task => mapper.Map<ProjectTaskViewModel, ProjectTaskPersistModel>(task)).ToArray();
        destination ??= new ProjectPersistModel();
        destination.Name = source.Name;
        destination.EditTime = source.EditTime;
        destination.Description = source.Description;
        destination.LanguageNames = source.LanguageNames?.ToArray();
        destination.FolderPath = source.FolderPath;
        destination.AllowedFolderPaths = source.AllowedFolderPaths?.ToArray();
        destination.TokensConsumption = source.TokensConsumption;
        destination.TotalPrice = source.TotalPrice;
        destination.Client = context.Mapper.Map<ILLMChatClient, LLMClientPersistModel>(source.Requester.DefaultClient);
        destination.Tasks = projectTaskPersistModels;
        return destination;
    }

    public ProjectTaskViewModel Convert(ProjectTaskPersistModel source, ProjectTaskViewModel? destination,
        ResolutionContext context)
    {
        if (!context.Items.TryGetValue(ParentProjectViewModelKey, out var parentProjectViewModel)
            || !(parentProjectViewModel is ProjectViewModel projectViewModel))
        {
            throw new InvalidOperationException("Parent ProjectViewModel is not set in context.");
        }

        destination ??= new ProjectTaskViewModel(projectViewModel);
        context.Items.Add(ParentSessionViewModelKey, destination);
        var mapper = context.Mapper;
        try
        {
            var sourceDialogItems = source.DialogItems?.Select<IDialogPersistItem, IDialogItem>((item =>
            {
                if (item is MultiResponsePersistItem multiResponsePersistItem)
                {
                    return mapper
                        .Map<MultiResponsePersistItem, MultiResponseViewItem>(multiResponsePersistItem);
                }

                if (item is RequestPersistItem requestViewItem)
                {
                    return mapper.Map<RequestPersistItem, RequestViewItem>(requestViewItem);
                }

                if (item is EraseViewItem eraseViewItem)
                {
                    return eraseViewItem;
                }

                throw new NotSupportedException();
            })).ToArray();
            if (sourceDialogItems != null)
            {
                foreach (var sourceDialogItem in sourceDialogItems)
                {
                    destination.DialogItems.Add(sourceDialogItem);
                }
            }

            destination.Name = source.Name;
            destination.Summary = source.Summary;
            destination.Description = source.Description;
            destination.Type = source.Type;
            destination.Status = source.Status;
            destination.TokensConsumption = source.TokensConsumption;
            destination.TotalPrice = source.TotalPrice;
            destination.SelectedFunctionGroups = source.AllowedFunctions?
                .Select((o => mapper.Map<AIFunctionGroupPersistObject, CheckableFunctionGroupTree>(o)))
                .ToArray();
        }
        finally
        {
            context.Items.Remove(ParentSessionViewModelKey);
        }

        return destination;
    }


    public ProjectTaskPersistModel Convert(ProjectTaskViewModel source, ProjectTaskPersistModel? destination,
        ResolutionContext context)
    {
        var mapper = context.Mapper;
        var dialogItems = source.DialogItems.Select<IDialogItem, IDialogPersistItem>(item =>
        {
            if (item is EraseViewItem eraseViewItem)
            {
                return eraseViewItem;
            }

            if (item is RequestViewItem requestViewItem)
            {
                return mapper.Map<RequestViewItem, RequestPersistItem>(requestViewItem);
            }

            if (item is MultiResponseViewItem multiResponseViewItem)
            {
                return mapper.Map<MultiResponseViewItem, MultiResponsePersistItem>(multiResponseViewItem);
            }

            throw new NotSupportedException();
        }).ToArray();
        destination ??= new ProjectTaskPersistModel();
        destination.Name = source.Name;
        destination.Summary = source.Summary;
        destination.Type = source.Type;
        destination.Status = source.Status;
        destination.DialogItems = dialogItems;
        destination.Description = source.Description;
        destination.TokensConsumption = source.TokensConsumption;
        destination.TotalPrice = source.TotalPrice;
        destination.AllowedFunctions = source.SelectedFunctionGroups?
            .Select(tree => mapper.Map<CheckableFunctionGroupTree, AIFunctionGroupPersistObject>(tree))
            .ToArray();
        return destination;
    }

    public LLMClientPersistModel Convert(ILLMChatClient source, LLMClientPersistModel? destination,
        ResolutionContext context)
    {
        var key = new ContextCacheKey(source, typeof(LLMClientPersistModel));
        if (context.InstanceCache?.TryGetValue(key,
                out var cachedValue) == true && cachedValue is LLMClientPersistModel cachedModel)
        {
            return cachedModel;
        }

        destination ??= new LLMClientPersistModel();
        destination.EndPointName = source.Endpoint.Name;
        destination.ModelName = source.Model.Name;
        destination.Params = source.Parameters;
        context.InstanceCache?.TryAdd(key, destination);
        return destination;
    }

    public ILLMChatClient Convert(LLMClientPersistModel source, ILLMChatClient? destination, ResolutionContext context)
    {
        if (destination != null)
        {
            throw new NotSupportedException("only create new ILLMClient");
        }

        var contextCacheKey = new ContextCacheKey(source, typeof(ILLMChatClient));
        if (context.InstanceCache?.TryGetValue(contextCacheKey, out var cachedValue) == true &&
            cachedValue is ILLMChatClient cachedClient)
        {
            return cachedClient;
        }

        var llmEndpoint = string.IsNullOrEmpty(source.EndPointName)
            ? null
            : _endpointService.GetEndpoint(source.EndPointName);
        var llmModelClient = llmEndpoint?.NewChatClient(source.ModelName) ?? NullLlmModelClient.Instance;
        var sourceJsonModel = source.Params;
        if (sourceJsonModel != null)
        {
            context.Mapper.Map(sourceJsonModel, llmModelClient.Parameters);
        }

        context.InstanceCache?.TryAdd(contextCacheKey, llmModelClient);
        return llmModelClient;
    }

    public AIFunctionGroupPersistObject Convert(CheckableFunctionGroupTree source,
        AIFunctionGroupPersistObject? destination,
        ResolutionContext context)
    {
        destination ??= new AIFunctionGroupPersistObject();
        destination.FunctionGroup = source.Data;
        destination.SelectedFunctionNames = source.Functions
            .Where(function => function.IsSelected)
            .Select(function => function.FunctionName!)
            .ToArray();
        return destination;
    }

    public CheckableFunctionGroupTree Convert(AIFunctionGroupPersistObject source,
        CheckableFunctionGroupTree? destination,
        ResolutionContext context)
    {
        var sourceFunctionGroup = source.FunctionGroup;
        var group = sourceFunctionGroup is McpServerItem server
            ? _mcpServiceCollection.TryGet(server)
            : sourceFunctionGroup;
        if (group == null)
        {
            throw new InvalidOperationException("Function group cannot be null.");
        }

        destination ??= new CheckableFunctionGroupTree(group);
        var virtualFunctionViewModels = source.SelectedFunctionNames
            ?.Select(s => new VirtualFunctionViewModel(s, destination) { IsSelected = true })
            .ToArray();
        destination.Functions.ResetWith(virtualFunctionViewModels ?? []);
        return destination;
    }
}