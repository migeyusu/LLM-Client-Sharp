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
    ITypeConverter<DialogViewModel, DialogFilePersistModel>,
    ITypeConverter<DialogFilePersistModel, DialogViewModel>,
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

    private IPromptsResource _promptsResource;

    public AutoMapModelTypeConverter(IEndpointService service, IMcpServiceCollection mcpServiceCollection,
        IViewModelFactory factory, IPromptsResource promptsResource)
    {
        this._endpointService = service;
        this._mcpServiceCollection = mcpServiceCollection;
        this._viewModelFactory = factory;
        _promptsResource = promptsResource;
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

    public DialogFilePersistModel Convert(DialogViewModel source, DialogFilePersistModel? destination,
        ResolutionContext context)
    {
        destination ??= new DialogFilePersistModel();
        var mapper = context.Mapper;
        var dialogItems = source.DialogItems
            .Select<IDialogItem, IDialogPersistItem>(item => mapper.Map<IDialogItem, IDialogPersistItem>(item))
            .ToArray();
        destination.DialogItems = dialogItems;
        destination.TokensConsumption = source.TokensConsumption;
        destination.TotalPrice = source.TotalPrice;
        destination.UserSystemPrompt = source.UserSystemPrompt;
        var sourceExtendedSystemPrompts = source.ExtendedSystemPrompts;
        if (sourceExtendedSystemPrompts.Any())
        {
            destination.ExtendedPrompts = new PromptsPersistModel()
            {
                PromptReference = sourceExtendedSystemPrompts.Select(entry => entry.Id).ToArray(),
            };
        }

        var requester = source.Requester;
        destination.PromptString = requester.PromptString;
        destination.AllowedFunctions = source.SelectedFunctionGroups
            ?.Select((tree => mapper.Map<CheckableFunctionGroupTree, AIFunctionGroupPersistObject>(tree))).ToArray();
        destination.Client = mapper.Map<ILLMChatClient, ParameterizedLLMModelPO>(requester.DefaultClient);
        return destination;
    }

    public const string ParentDialogViewModelKey = "ParentDialogViewModel";

    public const string ParentProjectViewModelKey = "ParentProjectViewModel";


    public DialogViewModel Convert(DialogFilePersistModel source, DialogViewModel? destination,
        ResolutionContext context)
    {
        var mapper = context.Mapper;
        var llmClient = source.Client == null
            ? EmptyLlmModelClient.Instance
            : mapper.Map<ParameterizedLLMModelPO, ILLMChatClient>(source.Client);
        if (destination != null)
        {
            destination.Topic = source.Topic;
            destination.Requester.DefaultClient = llmClient;
            destination.DialogItems.Clear();
        }
        else
        {
            destination = _viewModelFactory.CreateViewModel<DialogViewModel>(source.Topic, llmClient);
        }

        context.Items.Add(ParentDialogViewModelKey, destination);
        try
        {
            var sourceDialogItems = source.DialogItems
                ?.Select<IDialogPersistItem, IDialogItem>(item => mapper.Map<IDialogPersistItem, IDialogItem>(item))
                .ToArray();
            if (sourceDialogItems != null)
            {
                foreach (var sourceDialogItem in sourceDialogItems)
                {
                    destination.DialogItems.Add(sourceDialogItem);
                }
            }

            destination.TokensConsumption = source.TokensConsumption;
            destination.TotalPrice = source.TotalPrice;
            destination.UserSystemPrompt = source.UserSystemPrompt;
            var mapPrompts = MapPrompts(source.ExtendedPrompts, _promptsResource);
            if (mapPrompts != null)
            {
                destination.ExtendedSystemPrompts = mapPrompts;
            }

            var requester = destination.Requester;
            requester.PromptString = source.PromptString;
            destination.SelectedFunctionGroups = source.AllowedFunctions?.Select(o =>
                mapper.Map<AIFunctionGroupPersistObject, CheckableFunctionGroupTree>(o)).ToArray();
        }
        finally
        {
            context.Items.Remove(ParentDialogViewModelKey);
        }

        return destination;
    }


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

    private static IDialogItem? BuildTreeFromFlatList(IMapperBase mapper, IDialogPersistItem[]? flatPersistItems)
    {
        if (flatPersistItems == null || flatPersistItems.Length == 0) return null;

        var vmList = new List<IDialogItem>();
        var idMap = new Dictionary<Guid, IDialogItem>();
        // 临时变量，用于记录线性数组的上一个节点（处理老数据用）
        IDialogItem? lastImplicitParent = null;
        foreach (var persistItem in flatPersistItems)
        {
            // A.如果老数据没有 ID，现场分配一个，保证运行时有 ID
            if (persistItem.Id == Guid.Empty)
            {
                persistItem.Id = Guid.NewGuid();
            }

            // 转为 ViewModel (使用 AutoMapper)
            var dialogItem = mapper.Map<IDialogItem>(persistItem);

            // B. 确定 PreviousItemId
            // 如果文件里有 PreviousItemId (新数据 分叉)，用文件里的
            // 如果文件里没有 (老数据/主干)，默认认为是上一个 Item 的孩子
            var parentId = persistItem.PreviousItemId ?? lastImplicitParent?.Id;

            // C. 建立树关系
            if (parentId.HasValue && idMap.TryGetValue(parentId.Value, out var previousNode))
            {
                previousNode.AppendChild(dialogItem);
            }
            // 没有 parent 或者找不到 parent，说明是根节点
            // (通常只有数组第一个元素，或者断链的数据)

            // D. 注册到字典，供后续节点查找
            idMap[dialogItem.Id] = dialogItem;

            // E. 更新“隐式父节点”，为下一个循环做准备
            // 只有当这是老数据模式（即物理相邻表示逻辑相邻）时才更新
            // 如果是新数据的分叉节点（append在数组末尾的），逻辑上不一定是下一个节点的父，
            // 但为了保险，通常我们只对 ParentId 为 null 的情况使用这个 lastImplicitParent
            lastImplicitParent = dialogItem;
            vmList.Add(dialogItem);
        }

        // 返回所有的根节点（Parent 为空的节点）
        return vmList.FirstOrDefault(x => x.PreviousItem == null);
    }

    private IDialogPersistItem[] FlattenTreeForSave(IDialogItem root, IMapperBase mapper)
    {
        var flatList = new List<IDialogPersistItem>();
        var visited = new HashSet<Guid>();

        Visit(root);
        return flatList.ToArray();

        // 递归遍历（保证了父节点先加入列表）
        void Visit(IDialogItem item)
        {
            if (!visited.Add(item.Id)) return; // 防止循环引用死循环

            var persistItem = mapper.Map<IDialogPersistItem>(item);

            // 【关键】：写入 PreviousItemId
            persistItem.PreviousItemId = item.PreviousItem?.Id;

            // 这一步确保 ID 即使在 VM 运行时被新建，也能回写到 PO
            persistItem.Id = item.Id;

            flatList.Add(persistItem);

            foreach (var child in item.Children)
            {
                Visit(child);
            }
        }
    }
}