using System.Collections.ObjectModel;
using AutoMapper;
using LLMClient.Abstraction;
using LLMClient.Data;
using LLMClient.Endpoints;
using LLMClient.UI.Dialog;
using LLMClient.UI.MCP;
using LLMClient.UI.Project;

namespace LLMClient.UI;

public class AutoMapModelTypeConverter : ITypeConverter<DialogFileViewModel, DialogFilePersistModel>,
    ITypeConverter<DialogFilePersistModel, DialogFileViewModel>,
    ITypeConverter<DialogViewModel, DialogFilePersistModel>,
    ITypeConverter<DialogFilePersistModel, DialogViewModel>,
    ITypeConverter<MultiResponsePersistItem, MultiResponseViewItem>,
    ITypeConverter<MultiResponseViewItem, MultiResponsePersistItem>,
    ITypeConverter<ProjectPersistModel, ProjectViewModel>,
    ITypeConverter<ProjectViewModel, ProjectPersistModel>,
    ITypeConverter<ProjectTaskPersistModel, ProjectTask>,
    ITypeConverter<ProjectTask, ProjectTaskPersistModel>,
    ITypeConverter<ILLMClient, LLMClientPersistModel>,
    ITypeConverter<LLMClientPersistModel, ILLMClient>
{
    private readonly IEndpointService _endpointService;

    private readonly IMcpServiceCollection _mcpServiceCollection;

    public AutoMapModelTypeConverter(IEndpointService service, IMcpServiceCollection mcpServiceCollection)
    {
        this._endpointService = service;
        this._mcpServiceCollection = mcpServiceCollection;
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
        if (!context.Items.TryGetValue(ParentDialogViewModelKey, out var parentDialogViewModel)
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

        var dialogItems = source.DialogItems.Select<IDialogItem, IDialogPersistItem>(item =>
        {
            if (item is EraseViewItem eraseViewItem)
            {
                return eraseViewItem;
            }

            if (item is RequestViewItem requestViewItem)
            {
                return requestViewItem;
            }

            if (item is MultiResponseViewItem multiResponseViewItem)
            {
                return context.Mapper.Map<MultiResponseViewItem, MultiResponsePersistItem>(multiResponseViewItem);
            }

            throw new NotSupportedException();
        }).ToArray();
        destination.DialogItems = dialogItems;
        destination.TokensConsumption = source.TokensConsumption;
        destination.TotalPrice = source.TotalPrice;
        destination.SystemPrompt = source.SystemPrompt;
        var requester = source.Requester;
        destination.PromptString = requester.PromptString;
        destination.Functions = requester.FunctionSelector.SelectedFunctions.ToArray();
        destination.Client = context.Mapper.Map<ILLMClient, LLMClientPersistModel>(requester.DefaultClient);
        return destination;
    }

    private const string ParentDialogViewModelKey = "ParentDialogViewModel";

    private const string ParentProjectViewModelKey = "ParentProjectViewModel";

    private const string ParentProjectTaskViewModelKey = "ParentProjectTaskViewModel";

    public DialogViewModel Convert(DialogFilePersistModel source, DialogViewModel? destination,
        ResolutionContext context)
    {
        var llmClient = source.Client == null
            ? NullLlmModelClient.Instance
            : context.Mapper.Map<LLMClientPersistModel, ILLMClient>(source.Client);
        if (destination != null)
        {
            destination.Topic = source.Topic;
            destination.Requester.DefaultClient = llmClient;
            destination.DialogItems.Clear();
        }
        else
        {
            destination = new DialogViewModel(source.Topic, llmClient);
        }

        context.Items.Add(ParentDialogViewModelKey, destination);
        try
        {
            var sourceDialogItems = source.DialogItems?.Select<IDialogPersistItem, IDialogItem>((item =>
            {
                return item switch
                {
                    MultiResponsePersistItem multiResponsePersistItem => context.Mapper
                        .Map<MultiResponsePersistItem, MultiResponseViewItem>(multiResponsePersistItem),
                    RequestViewItem requestViewItem => requestViewItem,
                    EraseViewItem eraseViewItem => eraseViewItem,
                    _ => throw new NotSupportedException()
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
            var aiFunctionGroups = source.Functions?.Select((function) =>
            {
                if (function is McpServerItem server)
                {
                    return _mcpServiceCollection.TryGet(server);
                }

                return function;
            }).ToArray();
            requester.FunctionSelector.SelectedFunctions = aiFunctionGroups ?? [];
        }
        finally
        {
            context.Items.Remove(ParentDialogViewModelKey);
        }

        return destination;
    }

    public ProjectViewModel Convert(ProjectPersistModel source, ProjectViewModel? destination,
        ResolutionContext context)
    {
        var mapper = context.Mapper;
        var defaultClient = source.Client == null
            ? NullLlmModelClient.Instance
            : context.Mapper.Map<LLMClientPersistModel, ILLMClient>(source.Client);
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
                .Select(task => mapper.Map<ProjectTaskPersistModel, ProjectTask>(task))
                .ToArray();
            if (tasks != null)
            {
                foreach (var projectTask in tasks)
                {
                    destination.AddTask(projectTask);
                }
            }

            destination.EditTime = source.EditTime;
            destination.Name = source.Name;
            destination.Description = source.Description;
            destination.LanguageNames = source.LanguageNames;
            destination.FolderPath = source.FolderPath;
            destination.AllowedFolderPaths = source.AllowedFolderPaths == null
                ? []
                : new ObservableCollection<string>(source.AllowedFolderPaths);
            destination.TokensConsumption = source.TokensConsumption;
            destination.TotalPrice = source.TotalPrice;
            var aiFunctionGroups = source.AllowedFunctions?.Select((function) =>
            {
                if (function is McpServerItem server)
                {
                    return _mcpServiceCollection.TryGet(server);
                }

                return function;
            }).ToArray();
            destination.AllowedFunctions = aiFunctionGroups ?? [];
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
            ?.Select(task => mapper.Map<ProjectTask, ProjectTaskPersistModel>(task)).ToArray();
        destination ??= new ProjectPersistModel();
        destination.Name = source.Name;
        destination.EditTime = source.EditTime;
        destination.Description = source.Description;
        destination.LanguageNames = source.LanguageNames?.ToArray();
        destination.FolderPath = source.FolderPath;
        destination.AllowedFolderPaths = source.AllowedFolderPaths?.ToArray();
        destination.TokensConsumption = source.TokensConsumption;
        destination.TotalPrice = source.TotalPrice;
        destination.Client = context.Mapper.Map<ILLMClient, LLMClientPersistModel>(source.Requester.DefaultClient);
        destination.Tasks = projectTaskPersistModels;
        destination.AllowedFunctions = source.AllowedFunctions;

        return destination;
    }

    public ProjectTask Convert(ProjectTaskPersistModel source, ProjectTask? destination, ResolutionContext context)
    {
        if (!context.Items.TryGetValue(ParentProjectViewModelKey, out var parentProjectViewModel)
            || !(parentProjectViewModel is ProjectViewModel projectViewModel))
        {
            throw new InvalidOperationException("Parent ProjectViewModel is not set in context.");
        }

        destination ??= new ProjectTask(projectViewModel);
        context.Items.Add(ParentProjectTaskViewModelKey, destination);
        try
        {
            var sourceDialogItems = source.DialogItems?.Select<IDialogPersistItem, IDialogItem>((item =>
            {
                if (item is MultiResponsePersistItem multiResponsePersistItem)
                {
                    return context.Mapper
                        .Map<MultiResponsePersistItem, MultiResponseViewItem>(multiResponsePersistItem);
                }

                if (item is RequestViewItem requestViewItem)
                {
                    return requestViewItem;
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
        }
        finally
        {
            context.Items.Remove(ParentProjectTaskViewModelKey);
        }

        return destination;
    }


    public ProjectTaskPersistModel Convert(ProjectTask source, ProjectTaskPersistModel? destination,
        ResolutionContext context)
    {
        var dialogItems = source.DialogItems.Select<IDialogItem, IDialogPersistItem>(item =>
        {
            if (item is EraseViewItem eraseViewItem)
            {
                return eraseViewItem;
            }

            if (item is RequestViewItem requestViewItem)
            {
                return requestViewItem;
            }

            if (item is MultiResponseViewItem multiResponseViewItem)
            {
                return context.Mapper.Map<MultiResponseViewItem, MultiResponsePersistItem>(multiResponseViewItem);
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
        return destination;
    }

    public LLMClientPersistModel Convert(ILLMClient source, LLMClientPersistModel? destination,
        ResolutionContext context)
    {
        var key = new ContextCacheKey(source, typeof(LLMClientPersistModel));
        if (context.InstanceCache.TryGetValue(key,
                out var cachedValue) && cachedValue is LLMClientPersistModel cachedModel)
        {
            return cachedModel;
        }

        destination ??= new LLMClientPersistModel();
        destination.EndPointName = source.Endpoint.Name;
        destination.ModelName = source.Model.Name;
        destination.Params = source.Parameters;
        context.InstanceCache.TryAdd(key, destination);
        return destination;
    }

    public ILLMClient Convert(LLMClientPersistModel source, ILLMClient? destination, ResolutionContext context)
    {
        if (destination != null)
        {
            throw new NotSupportedException("only create new ILLMClient");
        }

        var contextCacheKey = new ContextCacheKey(source, typeof(ILLMClient));
        if (context.InstanceCache.TryGetValue(contextCacheKey, out var cachedValue) &&
            cachedValue is ILLMClient cachedClient)
        {
            return cachedClient;
        }

        var llmEndpoint = string.IsNullOrEmpty(source.EndPointName)
            ? null
            : _endpointService.GetEndpoint(source.EndPointName);
        var llmModelClient = llmEndpoint?.NewClient(source.ModelName) ?? NullLlmModelClient.Instance;
        var sourceJsonModel = source.Params;
        if (sourceJsonModel != null)
        {
            context.Mapper.Map(sourceJsonModel, llmModelClient.Parameters);
        }

        context.InstanceCache.TryAdd(contextCacheKey, llmModelClient);
        return llmModelClient;
    }
}