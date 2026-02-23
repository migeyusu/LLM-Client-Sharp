using AutoMapper;
using LLMClient.Abstraction;
using LLMClient.Component.ViewModel;
using LLMClient.Data;
using LLMClient.Dialog;
using LLMClient.Endpoints;
using LLMClient.ToolCall;
using LLMClient.ToolCall.MCP;

namespace LLMClient;

public class AutoMapModelTypeConverter : ITypeConverter<DialogFileViewModel, DialogFilePersistModel>,
    ITypeConverter<DialogFilePersistModel, DialogFileViewModel>,
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
        var dialogViewModel = context.Mapper.Map<DialogFilePersistModel, DialogViewModel>(source);
        if (destination != null)
        {
            throw new NotSupportedException("Cannot set dialogViewModel to exist 'DialogSession' instance");
        }

        var viewModel = _viewModelFactory.CreateViewModel<DialogFileViewModel>(dialogViewModel);
        viewModel.EditTime = source.EditTime;
        return viewModel;
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
}