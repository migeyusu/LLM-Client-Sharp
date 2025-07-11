using System.Collections.ObjectModel;
using AutoMapper;
using LLMClient.Abstraction;
using LLMClient.Data;
using LLMClient.Endpoints;
using LLMClient.UI.Dialog;
using LLMClient.UI.MCP;
using LLMClient.UI.Project;
using Microsoft.Extensions.Azure;

namespace LLMClient.UI;

public class AutoMapModelTypeConverter : ITypeConverter<DialogSession, DialogSessionPersistModel>,
    ITypeConverter<DialogViewModel, DialogPersistModel>,
    ITypeConverter<DialogPersistModel, DialogViewModel>,
    ITypeConverter<DialogSessionPersistModel, DialogSession>,
    ITypeConverter<MultiResponsePersistItem, MultiResponseViewItem>,
    ITypeConverter<MultiResponseViewItem, MultiResponsePersistItem>,
    ITypeConverter<ResponsePersistItem, ResponseViewItem>,
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

    public DialogSessionPersistModel Convert(DialogSession from, DialogSessionPersistModel? destination,
        ResolutionContext context)
    {
        destination ??= new DialogSessionPersistModel();
        var source = from.Dialog;
        context.Mapper.Map<DialogViewModel, DialogPersistModel>(source, destination);
        destination.EditTime = from.EditTime;
        return destination;
    }

    public DialogSession Convert(DialogSessionPersistModel source, DialogSession? destination,
        ResolutionContext context)
    {
        var dialogViewModel = context.Mapper.Map<DialogPersistModel, DialogViewModel>(source);
        if (destination != null)
        {
            throw new NotSupportedException("Cannot set dialogViewModel to exist DialogSession");
        }

        return new DialogSession(dialogViewModel) { EditTime = source.EditTime };
    }

    public ResponseViewItem Convert(ResponsePersistItem source, ResponseViewItem? destination,
        ResolutionContext context)
    {
        if (destination == null)
        {
            throw new NotSupportedException("Cannot set ResponsePersistItem to exist ResponseViewItem");
        }

        ILLMClient llmClient = NullLlmModelClient.Instance;
        var client = source.Client;
        if (client != null)
        {
            llmClient = context.Mapper.Map<LLMClientPersistModel, ILLMClient>(client);
        }

        var responseViewItem = new ResponseViewItem(llmClient);
        context.Mapper.Map<IResponse, ResponseViewItem>(source, responseViewItem);
        return responseViewItem;
    }

    public MultiResponseViewItem Convert(MultiResponsePersistItem source, MultiResponseViewItem? destination,
        ResolutionContext context)
    {
        var items = source.ResponseItems.Select(x =>
            context.Mapper.Map<ResponsePersistItem, ResponseViewItem>(x));
        if (destination != null)
        {
            destination.Items = new ObservableCollection<IResponseViewItem>(items);
            destination.AcceptedIndex = source.AcceptedIndex;
            destination.InteractionId = source.InteractionId;
            return destination;
        }

        return new MultiResponseViewItem(items)
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

    public DialogPersistModel Convert(DialogViewModel source, DialogPersistModel? destination,
        ResolutionContext context)
    {
        if (destination == null)
        {
            destination = new DialogPersistModel();
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
        destination.Topic = source.Topic;
        var clientPersistModel = context.Mapper.Map<ILLMClient, LLMClientPersistModel>(source.DefaultClient);
        destination.Client = clientPersistModel;
        destination.PromptString = source.PromptString;
        destination.TokensConsumption = source.TokensConsumption;
        destination.TotalPrice = source.TotalPrice;
        destination.Functions = source.SelectedFunctions?.ToArray();
        destination.SystemPrompt = source.SystemPrompt;
        return destination;
    }

    public DialogViewModel Convert(DialogPersistModel source, DialogViewModel? destination, ResolutionContext context)
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
        var llmClient = source.Client == null
            ? NullLlmModelClient.Instance
            : context.Mapper.Map<LLMClientPersistModel, ILLMClient>(source.Client);

        var aiFunctionGroups = source.Functions?.Select((function) =>
        {
            if (function is McpServerItem server)
            {
                return _mcpServiceCollection.TryGet(server);
            }

            return function;
        }).ToArray();
        if (destination != null)
        {
            destination.Topic = source.Topic;
            destination.DefaultClient = llmClient;
            destination.DialogItems.Clear();
            if (sourceDialogItems != null)
            {
                foreach (var sourceDialogItem in sourceDialogItems)
                {
                    destination.DialogItems.Add(sourceDialogItem);
                }
            }
        }
        else
        {
            destination = new DialogViewModel(source.Topic, llmClient, sourceDialogItems);
        }

        destination.SystemPrompt = source.SystemPrompt;
        destination.PromptString = source.PromptString;
        destination.TokensConsumption = source.TokensConsumption;
        destination.TotalPrice = source.TotalPrice;
        destination.SelectedFunctions = aiFunctionGroups;
        return destination;
    }

    public ProjectViewModel Convert(ProjectPersistModel source, ProjectViewModel? destination,
        ResolutionContext context)
    {
        var aiFunctionGroups = source.AllowedFunctions?.Select((function) =>
        {
            if (function is McpServerItem server)
            {
                return _mcpServiceCollection.TryGet(server);
            }

            return function;
        }).ToArray();
        var mapper = context.Mapper;
        var tasks = source.Tasks?
            .Select((task) => mapper.Map<ProjectTaskPersistModel, ProjectTask>(task))
            .ToArray();
        if (destination != null)
        {
            destination.Tasks.Clear();
            if (tasks != null)
            {
                foreach (var projectTask in tasks)
                {
                    destination.Tasks.Add(projectTask);
                }
            }
        }
        else
        {
            destination = new ProjectViewModel(tasks);
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
        destination.AllowedFunctions = aiFunctionGroups;
        destination.Client = source.Client == null
            ? NullLlmModelClient.Instance
            : context.Mapper.Map<LLMClientPersistModel, ILLMClient>(source.Client);
        return destination;
    }

    public ProjectPersistModel Convert(ProjectViewModel source, ProjectPersistModel? destination,
        ResolutionContext context)
    {
        var mapper = context.Mapper;
        var projectTaskPersistModels = source.Tasks
            ?.Select((task => mapper.Map<ProjectTask, ProjectTaskPersistModel>(task))).ToArray();
        destination ??= new ProjectPersistModel();
        destination.Name = source.Name;
        destination.Description = source.Description;
        destination.LanguageNames = source.LanguageNames?.ToArray();
        destination.FolderPath = source.FolderPath;
        destination.AllowedFolderPaths = source.AllowedFolderPaths?.ToArray();
        destination.TokensConsumption = source.TokensConsumption;
        destination.TotalPrice = source.TotalPrice;
        destination.Client = context.Mapper.Map<ILLMClient, LLMClientPersistModel>(source.Client);
        destination.Tasks = projectTaskPersistModels;
        destination.AllowedFunctions = source.AllowedFunctions;
        return destination;
    }

    public ProjectTask Convert(ProjectTaskPersistModel source, ProjectTask? destination, ResolutionContext context)
    {
        var sourceDialogItems = source.DialogItems?.Select<IDialogPersistItem, IDialogItem>((item =>
        {
            if (item is MultiResponsePersistItem multiResponsePersistItem)
            {
                return context.Mapper.Map<MultiResponsePersistItem, MultiResponseViewItem>(multiResponsePersistItem);
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
        destination ??= new ProjectTask(sourceDialogItems);
        destination.Name = source.Name;
        destination.Summary = source.Summary;
        destination.Type = source.Type;
        destination.Status = source.Status;
        destination.SystemPrompt = source.SystemPrompt;
        destination.PromptString = source.PromptString;
        destination.TokensConsumption = source.TokensConsumption;
        destination.TotalPrice = source.TotalPrice;
        return destination;
    }

    public ProjectTaskPersistModel Convert(ProjectTask source, ProjectTaskPersistModel? destination,
        ResolutionContext context)
    {
        var dialogItems = source.DialogItems.Select<IDialogItem, IDialogPersistItem>((item =>
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
        })).ToArray();
        destination ??= new ProjectTaskPersistModel();
        destination.Name = source.Name;
        destination.Summary = source.Summary;
        destination.Type = source.Type;
        destination.Status = source.Status;
        destination.DialogItems = dialogItems;
        destination.PromptString = source.PromptString;
        destination.TokensConsumption = source.TokensConsumption;
        destination.TotalPrice = source.TotalPrice;
        destination.SystemPrompt = source.SystemPrompt;
        return destination;
    }

    public LLMClientPersistModel Convert(ILLMClient source, LLMClientPersistModel? destination,
        ResolutionContext context)
    {
        destination ??= new LLMClientPersistModel();
        destination.EndPointName = source.Endpoint.Name;
        destination.ModelName = source.Model.Name;
        destination.Params = source.Parameters;
        return destination;
    }

    public ILLMClient Convert(LLMClientPersistModel source, ILLMClient? destination, ResolutionContext context)
    {
        if (destination != null)
        {
            throw new NotSupportedException("only create new ILLMClient");
        }

        var llmEndpoint = string.IsNullOrEmpty(source.EndPointName)
            ? null
            : _endpointService.GetEndpoint(source.EndPointName);
        var llmModelClient = llmEndpoint?.NewClient(source.ModelName) ?? NullLlmModelClient.Instance;
        var sourceJsonModel = source.Params;
        if (sourceJsonModel != null)
        {
            context.Mapper.Map<IModelParams, IModelParams>(sourceJsonModel, llmModelClient.Parameters);
        }

        return llmModelClient;
    }
}