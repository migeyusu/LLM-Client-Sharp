using System.Collections.ObjectModel;
using System.Diagnostics;
using AutoMapper;
using LLMClient.Abstraction;
using LLMClient.Component.ViewModel;
using LLMClient.Configuration;
using LLMClient.Data;
using LLMClient.Dialog;
using LLMClient.Dialog.Models;
using LLMClient.Endpoints;
using LLMClient.Endpoints.OpenAIAPI;
using LLMClient.Project;
using LLMClient.ToolCall;
using Microsoft.Extensions.AI;

namespace LLMClient;

public class DialogMappingProfile : Profile
{
    private readonly IViewModelFactory _viewModelFactory;

    public DialogMappingProfile(IViewModelFactory viewModelFactory, IPromptsResource promptsResource)
    {
        _viewModelFactory = viewModelFactory;
        CreateMap<CheckableFunctionGroupTree, AIFunctionGroupPersistObject>()
            .ConvertUsing<AutoMapModelTypeConverter>();
        CreateMap<AIFunctionGroupPersistObject, CheckableFunctionGroupTree>()
            .ConvertUsing<AutoMapModelTypeConverter>();
        CreateMap<IThinkingConfig, GeekAIThinkingConfig>();
        CreateMap<IThinkingConfig, OpenRouterReasoningConfig>();
        CreateMap<IThinkingConfig, NVDAAPIThinkingConfig>();
        CreateMap<IAIContent, AIContent>().IncludeAllDerived();
        CreateMap<AIContent, IAIContent>().IncludeAllDerived();
        CreateMap<ChatMessage, ChatMessagePO>();
        CreateMap<ChatMessagePO, ChatMessage>();
        CreateMap<TextContent, TextContentPO>();
        CreateMap<TextContentPO, TextContent>();
        CreateMap<FunctionCallContent, FunctionCallContentPO>();
        CreateMap<FunctionCallContentPO, FunctionCallContent>();
        CreateMap<DataContent, DataContentPO>()
            .ForMember(po => po.Data, opt => opt.MapFrom(content => content.Data));
        CreateMap<DataContentPO, DataContent>()
            .ConstructUsing((po, _) =>
            {
                if (po.Data != null)
                {
                    return new DataContent(po.Data, po.MediaType);
                }

                if (po.Uri != null)
                {
                    return new DataContent(po.Uri, po.MediaType);
                }

                throw new InvalidOperationException();
            });
        CreateMap<ErrorContent, ErrorContentPO>();
        CreateMap<ErrorContentPO, ErrorContent>();
        CreateMap<FunctionResultContent, FunctionResultContentPO>();
        CreateMap<FunctionResultContentPO, FunctionResultContent>()
            .ConstructUsing((po, _) => new FunctionResultContent(po.CallId, po.Result)
                { Exception = po.Exception });
        CreateMap<TextReasoningContent, TextReasoningContentPO>();
        CreateMap<TextReasoningContentPO, TextReasoningContent>();
        CreateMap<UriContent, UriContentPO>();
        CreateMap<UriContentPO, UriContent>()
            .ConstructUsing(po => new UriContent(po.Uri!, po.MediaType!));
        CreateMap<UsageContent, UsageContentPO>();
        CreateMap<UsageContentPO, UsageContent>();

        const string parentDialogViewModelKey = AutoMapModelTypeConverter.ParentDialogViewModelKey;

        //vm -> po
        CreateMap<IDialogItem, IDialogPersistItem>()
            .Include<RequestViewItem, RequestPersistItem>()
            .Include<SummaryRequestViewItem, SummaryRequestPersistItem>()
            .Include<EraseViewItem, ErasePersistItem>()
            .Include<MultiResponseViewItem, MultiResponsePersistItem>();

        CreateMap<RequestViewItem, RequestPersistItem>()
            .IncludeBase<IDialogItem, IDialogPersistItem>();
        CreateMap<SummaryRequestViewItem, SummaryRequestPersistItem>()
            .IncludeBase<IDialogItem, IDialogPersistItem>();
        CreateMap<EraseViewItem, ErasePersistItem>()
            .IncludeBase<IDialogItem, IDialogPersistItem>();
        CreateMap<MultiResponseViewItem, MultiResponsePersistItem>()
            .IncludeBase<IDialogItem, IDialogPersistItem>()
            .ForMember(item => item.ResponseItems, opt => opt.MapFrom(item => item.Items));

        //po -> vm
        CreateMap<IDialogPersistItem, IDialogItem>()
            .Include<RequestPersistItem, RequestViewItem>()
            .Include<SummaryRequestPersistItem, SummaryRequestViewItem>()
            .Include<ErasePersistItem, EraseViewItem>()
            .Include<MultiResponsePersistItem, MultiResponseViewItem>();

        CreateMap<RequestPersistItem, RequestViewItem>()
            .ConstructUsing((item, context) =>
            {
                var contextItems = context.Items;
                if (!contextItems.TryGetValue(parentDialogViewModelKey,
                        out var parentDialogViewModel)
                    || parentDialogViewModel is not DialogSessionViewModel parentViewModel)
                {
                    throw new InvalidOperationException("Parent DialogViewModel is not set in context.");
                }

                return new RequestViewItem(item.RawTextMessage ?? string.Empty, parentViewModel);
            });
        CreateMap<SummaryRequestPersistItem, SummaryRequestViewItem>()
            .IncludeBase<IDialogPersistItem, IDialogItem>();
        CreateMap<ErasePersistItem, EraseViewItem>()
            .IncludeBase<IDialogPersistItem, IDialogItem>();
        CreateMap<MultiResponsePersistItem, MultiResponseViewItem>()
            .IncludeBase<IDialogPersistItem, IDialogItem>()
            .ConstructUsing((item, context) =>
            {
                var contextItems = context.Items;
                if (!contextItems.TryGetValue(parentDialogViewModelKey, out var parentDialogViewModel)
                    || !(parentDialogViewModel is DialogSessionViewModel parentViewModel))
                {
                    throw new InvalidOperationException("Parent DialogViewModel is not set in context.");
                }

                return new MultiResponseViewItem(parentViewModel);
            })
            .AfterMap((source, destination, context) =>
            {
                var contextMapper = context.Mapper;
                var items = source.ResponseItems.Select(x =>
                    contextMapper.Map<ResponsePersistItem, ResponseViewItem>(x)).ToArray();
                var responseViewItems = destination.Items;
                foreach (var item in items)
                {
                    responseViewItems.Add(item);
                }
            });

        CreateMap<IResponse, ResponseViewItem>();
        CreateMap<IModelParams, IModelParams>();
        CreateMap<IModelParams, DefaultModelParam>();
        CreateMap<DefaultModelParam, DefaultModelParam>();
        CreateMap<IModelParams, IEndpointModel>();
        CreateMap<IEndpointModel, IModelParams>();
        CreateMap<IModelParams, APIModelInfo>();
        CreateMap<APIDefaultOption, APIDefaultOption>();
        CreateMap<ILLMChatClient, ParameterizedLLMModelPO>()
            .ConvertUsing<AutoMapModelTypeConverter>();
        CreateMap<IParameterizedLLMModel, ParameterizedLLMModelPO>()
            .ConvertUsing<AutoMapModelTypeConverter>();
        CreateMap<ParameterizedLLMModelPO, IParameterizedLLMModel>()
            .ConvertUsing<AutoMapModelTypeConverter>();
        CreateMap<ParameterizedLLMModelPO, ILLMChatClient>()
            .ConvertUsing<AutoMapModelTypeConverter>();
        CreateMap<DialogFilePersistModel, DialogFileViewModel>()
            .ConvertUsing<AutoMapModelTypeConverter>();
        CreateMap<DialogFileViewModel, DialogFilePersistModel>()
            .ConvertUsing<AutoMapModelTypeConverter>();

        CreateMap<ResponseViewItem, ResponsePersistItem>()
            .PreserveReferences();
        CreateMap<ResponsePersistItem, ResponseViewItem>()
            .PreserveReferences()
            .ConstructUsing((source, context) =>
            {
                ILLMChatClient llmClient = EmptyLlmModelClient.Instance;
                var client = source.Client;
                if (client != null)
                {
                    llmClient = context.Mapper.Map<ParameterizedLLMModelPO, ILLMChatClient>(client);
                }

                return new ResponseViewItem(llmClient);
            });


        CreateMap<ProjectOptionsPersistModel, ProjectOption>();
        CreateMap<ProjectOption, ProjectOptionsPersistModel>();

        #region project

        //PO -> VM
        CreateMap<ProjectPersistModel, ProjectViewModel>()
            .Include<GeneralProjectPersistModel, GeneralProjectViewModel>()
            .Include<CSharpProjectPersistModel, CSharpProjectViewModel>()
            .Include<CppProjectPersistModel, CppProjectViewModel>()
            .ForMember(dest => dest.Tasks, opt => opt.Ignore())
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
            .ConstructUsing(CreateViewModel<CSharpProjectViewModel>);
        CreateMap<CppProjectPersistModel, CppProjectViewModel>()
            .IncludeBase<ProjectPersistModel, ProjectViewModel>()
            .ConstructUsing(CreateViewModel<CppProjectViewModel>);

        //VM -> PO
        CreateMap<ProjectViewModel, ProjectPersistModel>()
            .Include<GeneralProjectViewModel, GeneralProjectPersistModel>()
            .Include<CSharpProjectViewModel, CSharpProjectPersistModel>()
            .Include<CppProjectViewModel, CppProjectPersistModel>()
            .ForMember(dest => dest.Client, opt => opt.MapFrom(src => src.Requester.DefaultClient))
            .ForMember(dest => dest.ExtendedPrompts,
                opt =>
                {
                    opt.PreCondition(src => src.ExtendedSystemPrompts.Any());
                    opt.MapFrom(src => new PromptsPersistModel()
                        {
                            PromptReference = src.ExtendedSystemPrompts.Select(entry => entry.Id).ToArray(),
                        }
                    );
                })
            .ForMember(dest => dest.UserPrompt, opt => opt.MapFrom(src => src.Requester.PromptString));
        CreateMap<GeneralProjectViewModel, GeneralProjectPersistModel>()
            .IncludeBase<ProjectViewModel, ProjectPersistModel>();
        CreateMap<CSharpProjectViewModel, CSharpProjectPersistModel>()
            .IncludeBase<ProjectViewModel, ProjectPersistModel>();
        CreateMap<CppProjectViewModel, CppProjectPersistModel>()
            .IncludeBase<ProjectViewModel, ProjectPersistModel>();

        #endregion

        #region dialog session viewmodel

        //VM -> PO
        CreateMap<DialogSessionViewModel, DialogSessionPersistModel>()
            .Include<DialogViewModel, DialogFilePersistModel>()
            .Include<ProjectSessionViewModel, ProjectSessionPersistModel>()
            .ForMember(model => model.AllowedFunctions,
                expression => expression.MapFrom((model => model.SelectedFunctionGroups)))
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
                expression.MapFrom(s => new PromptsPersistModel()
                {
                    PromptReference = s.ExtendedSystemPrompts.Select(entry => entry.Id).ToArray(),
                });
            })
            .ForMember(model => model.PromptString, opt => opt.MapFrom(src => src.Requester.PromptString))
            .ForMember(model => model.Client, opt => opt.MapFrom(src => src.Requester.DefaultClient));

        CreateMap<ProjectSessionViewModel, ProjectSessionPersistModel>()
            .IncludeBase<DialogSessionViewModel, DialogSessionPersistModel>();

        // PO -> VM
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
                    var runtimeMapper = ctx.Mapper;
                    // 设置子节点需要的上下文
                    ctx.Items[parentDialogViewModelKey] = dest;
                    BuildTreeFromFlatList(runtimeMapper, src, dest);
                }
                finally
                {
                    // 清理上下文防止污染其他不相关的映射
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
                return _viewModelFactory.CreateViewModel<DialogViewModel>(model.Topic, llmClient);
            })
            //destination.Requester.DefaultClient
            .ForMember(dest => dest.ExtendedSystemPrompts,
                opt =>
                {
                    opt.PreCondition(src => src.ExtendedPrompts != null);
                    opt.MapFrom(src => MapPrompts(src.ExtendedPrompts, promptsResource));
                })
            .AfterMap((src, dest, ctx) => { dest.Requester.PromptString = src.PromptString; });

        CreateMap<ProjectSessionPersistModel, ProjectSessionViewModel>()
            .IncludeBase<DialogSessionPersistModel, DialogSessionViewModel>()
            .ConstructUsing((src, ctx) =>
            {
                if (!ctx.Items.TryGetValue(AutoMapModelTypeConverter.ParentProjectViewModelKey,
                        out var parentProjectViewModel)
                    || parentProjectViewModel is not ProjectViewModel projectViewModel)
                {
                    throw new InvalidOperationException("Parent ProjectViewModel is not set in context.");
                }

                return viewModelFactory.CreateViewModel<ProjectSessionViewModel>(projectViewModel);
            });

        #endregion
    }

    /// <summary>
    /// 统一的构造方法 - 不再需要类型判断
    /// </summary>
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
        return _viewModelFactory.CreateViewModel<TViewModel>(projectOption, client);
    }

    /// <summary>
    /// 通用的后置处理 - 所有类型共享
    /// </summary>
    private static void AfterMapProjectViewModel(ProjectPersistModel source, ProjectViewModel dest,
        ResolutionContext context)
    {
        dest.Requester.PromptString = source.UserPrompt;
        context.Items[AutoMapModelTypeConverter.ParentProjectViewModelKey] = dest;

        try
        {
            if (source.Tasks != null && source.Tasks.Length != 0)
            {
                dest.Tasks.Clear();
                var tasks = context.Mapper.Map<IEnumerable<ProjectSessionViewModel>>(source.Tasks);
                foreach (var task in tasks)
                {
                    dest.AddTask(task);
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
        var flatPersistItems = source.DialogItems;
        if (flatPersistItems == null || flatPersistItems.Length == 0) return;

        var idMap = new Dictionary<Guid, IDialogItem>();
        // 临时变量，用于记录线性数组的上一个节点（处理老数据用）
        IDialogItem? lastImplicitParent = null;
        for (var index = 0; index < flatPersistItems.Length; index++)
        {
            var persistItem = flatPersistItems[index];
            // A.如果老数据没有 ID，现场分配一个，保证运行时有 ID
            if (persistItem.Id == Guid.Empty)
            {
                persistItem.Id = Guid.NewGuid();
            }

            // 转为 ViewModel (使用 AutoMapper)
            var dialogItem = mapper.Map<IDialogPersistItem, IDialogItem>(persistItem);

            // B. 确定 PreviousItemId
            // 如果文件里有 PreviousItemId (新数据 分叉)，用文件里的
            // 如果文件里没有 (老数据/主干)，默认认为是上一个 Item 的孩子
            var previousItemId = persistItem.PreviousItemId ?? lastImplicitParent?.Id;

            // C. 建立树关系
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
            // 没有 parent 或者找不到 parent，说明是根节点 (通常只有数组第一个元素，或者断链的数据)
            // 由于每个对话根都是隐式节点，所以头默认会找不到前向节点

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