using System.Collections.ObjectModel;
using AutoMapper;
using LLMClient.Abstraction;
using LLMClient.Component.ViewModel;
using LLMClient.Configuration;
using LLMClient.Data;
using LLMClient.Dialog;
using LLMClient.Endpoints;
using LLMClient.ToolCall;

namespace LLMClient;

public class AutoMapModelTypeConverter : ITypeConverter<DialogFileViewModel, DialogFilePersistModel>,
    ITypeConverter<DialogFilePersistModel, DialogFileViewModel>,
    ITypeConverter<MultiResponsePersistItem, MultiResponseViewItem>,
    ITypeConverter<MultiResponseViewItem, MultiResponsePersistItem>,
    ITypeConverter<ILLMChatClient, ParameterizedLLMModelPO>,
    ITypeConverter<ParameterizedLLMModelPO, ILLMChatClient>,
    ITypeConverter<CheckableFunctionGroupTree, AIFunctionGroupPersistObject>,
    ITypeConverter<AIFunctionGroupPersistObject, CheckableFunctionGroupTree>,
    ITypeConverter<IParameterizedLLMModel, ParameterizedLLMModelPO>,
    ITypeConverter<ParameterizedLLMModelPO, IParameterizedLLMModel>
{
    private readonly IEndpointService _endpointService;

    private readonly IMcpServiceCollection _mcpServiceCollection;

    private readonly IViewModelFactory _viewModelFactory;

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
            throw new NotSupportedException("Cannot set dialogViewModel to exist 'DialogSession' instance");
        }

        var viewModel = _viewModelFactory.CreateViewModel<DialogFileViewModel>(dialogViewModel);
        viewModel.EditTime = source.EditTime;
        return viewModel;
    }

    public MultiResponseViewItem Convert(MultiResponsePersistItem source, MultiResponseViewItem? destination,
        ResolutionContext context)
    {
        var contextItems = context.Items;
        if (!contextItems.TryGetValue(ParentDialogViewModelKey, out var parentDialogViewModel)
            || !(parentDialogViewModel is DialogSessionViewModel parentViewModel))
        {
            throw new InvalidOperationException("Parent DialogViewModel is not set in context.");
        }

        var items = source.ResponseItems.Select(x =>
            context.Mapper.Map<ResponsePersistItem, ResponseViewItem>(x)).ToArray();
        if (destination == null)
        {
            destination = new MultiResponseViewItem(items, parentViewModel);
        }
        else
        {
            var responseViewItems = destination.Items;
            foreach (var item in items)
            {
                responseViewItems.Add(item);
            }
        }

        destination.AcceptedResponse = items.Length <= 0 ? null : items[Math.Max(source.AcceptedIndex, 0)];
        destination.InteractionId = source.InteractionId;
        return destination;
    }

    public MultiResponsePersistItem Convert(MultiResponseViewItem source, MultiResponsePersistItem? destination,
        ResolutionContext context)
    {
        destination ??= new MultiResponsePersistItem();
        destination.AcceptedIndex = source.AcceptedResponse != null
            ? source.Items.IndexOf(source.AcceptedResponse)
            : source.Items.Count - 1;
        destination.ResponseItems = source.Items
            .Select(x => context.Mapper.Map<ResponseViewItem, ResponsePersistItem>(x))
            .ToArray();
        destination.InteractionId = source.InteractionId;
        return destination;
    }

    public const string ParentDialogViewModelKey = "ParentDialogViewModel";

    public const string ParentProjectViewModelKey = "ParentProjectViewModel";

    public ParameterizedLLMModelPO Convert(ILLMChatClient source, ParameterizedLLMModelPO? destination,
        ResolutionContext context)
    {
        var key = new ContextCacheKey(source, typeof(ParameterizedLLMModelPO));
        if (context.InstanceCache?.TryGetValue(key,
                out var cachedValue) == true && cachedValue is ParameterizedLLMModelPO cachedModel)
        {
            return cachedModel;
        }

        destination ??= new ParameterizedLLMModelPO();
        destination.EndPointName = source.Endpoint.Name;
        destination.ModelName = source.Model.Name;
        destination.Parameters = source.Parameters;
        context.InstanceCache?.TryAdd(key, destination);
        return destination;
    }

    public ILLMChatClient Convert(ParameterizedLLMModelPO source, ILLMChatClient? destination,
        ResolutionContext context)
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
        var llmModelClient = llmEndpoint?.GetModel(source.ModelName)?
            .CreateChatClient() ?? EmptyLlmModelClient.Instance;
        var sourceJsonModel = source.Parameters;
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

    public ParameterizedLLMModelPO Convert(IParameterizedLLMModel source, ParameterizedLLMModelPO? destination,
        ResolutionContext context)
    {
        var key = new ContextCacheKey(source, typeof(ParameterizedLLMModelPO));
        if (context.InstanceCache?.TryGetValue(key,
                out var cachedValue) == true && cachedValue is ParameterizedLLMModelPO cachedModel)
        {
            return cachedModel;
        }

        destination ??= new ParameterizedLLMModelPO();
        destination.EndPointName = source.Model.Endpoint.Name;
        destination.ModelName = source.Model.Name;
        destination.Parameters = source.Parameters;
        context.InstanceCache?.TryAdd(key, destination);
        return destination;
    }

    public IParameterizedLLMModel Convert(ParameterizedLLMModelPO source, IParameterizedLLMModel? destination,
        ResolutionContext context)
    {
        if (destination != null)
        {
            throw new NotSupportedException("only create new IParameterizedLLMModel");
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
        var llmModelClient = llmEndpoint?.GetModel(source.ModelName)?
            .CreateChatClient() ?? EmptyLlmModelClient.Instance;
        var sourceJsonModel = source.Parameters;
        if (sourceJsonModel != null)
        {
            context.Mapper.Map(sourceJsonModel, llmModelClient.Parameters);
        }

        context.InstanceCache?.TryAdd(contextCacheKey, llmModelClient);
        return llmModelClient;
    }

    public static ObservableCollection<PromptEntry>? MapPrompts(PromptsPersistModel? sourceExtendedPrompts,
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
}