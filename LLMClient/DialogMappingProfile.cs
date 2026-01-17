using System.Collections.ObjectModel;
using AutoMapper;
using LLMClient.Abstraction;
using LLMClient.Component.ViewModel;
using LLMClient.Configuration;
using LLMClient.Data;
using LLMClient.Dialog;
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
            .ConstructUsing(((po, _) =>
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
            }));
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
            .ConstructUsing((po => new UriContent(po.Uri!, po.MediaType!)));
        CreateMap<UsageContent, UsageContentPO>();
        CreateMap<UsageContentPO, UsageContent>();
        CreateMap<RequestViewItem, RequestPersistItem>();
        CreateMap<RequestPersistItem, RequestViewItem>()
            .ConstructUsing((item, context) =>
            {
                var contextItems = context.Items;
                if (!contextItems.TryGetValue(AutoMapModelTypeConverter.ParentDialogViewModelKey,
                        out var parentDialogViewModel)
                    || parentDialogViewModel is not DialogSessionViewModel parentViewModel)
                {
                    throw new InvalidOperationException("Parent DialogViewModel is not set in context.");
                }

                return new RequestViewItem(item.RawTextMessage ?? string.Empty, parentViewModel);
            });

        CreateMap<SummaryRequestViewItem, SummaryRequestPersistItem>();
        CreateMap<SummaryRequestPersistItem, SummaryRequestViewItem>();
        CreateMap<EraseViewItem, ErasePersistItem>();
        CreateMap<ErasePersistItem, EraseViewItem>();
        CreateMap<IDialogItem, IDialogPersistItem>()
            .Include<RequestViewItem, RequestPersistItem>()
            .Include<SummaryRequestViewItem, SummaryRequestPersistItem>()
            .Include<EraseViewItem, ErasePersistItem>()
            .Include<MultiResponseViewItem, MultiResponsePersistItem>();
        CreateMap<IDialogPersistItem, IDialogItem>()
            .Include<RequestPersistItem, RequestViewItem>()
            .Include<SummaryRequestPersistItem, SummaryRequestViewItem>()
            .Include<ErasePersistItem, EraseViewItem>()
            .Include<MultiResponsePersistItem, MultiResponseViewItem>();
        CreateMap<IResponse, ResponseViewItem>();
        CreateMap<IModelParams, IModelParams>();
        CreateMap<IModelParams, DefaultModelParam>();
        CreateMap<DefaultModelParam, DefaultModelParam>();
        CreateMap<IModelParams, ILLMModel>();
        CreateMap<ILLMModel, IModelParams>();
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
        CreateMap<DialogViewModel, DialogFilePersistModel>()
            .ConvertUsing<AutoMapModelTypeConverter>();
        CreateMap<DialogFilePersistModel, DialogViewModel>()
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
        CreateMap<MultiResponsePersistItem, MultiResponseViewItem>()
            .ConvertUsing<AutoMapModelTypeConverter>();
        CreateMap<MultiResponseViewItem, MultiResponsePersistItem>()
            .ConvertUsing<AutoMapModelTypeConverter>();
        CreateMap<ProjectOptionsPersistModel, ProjectOption>();
        CreateMap<ProjectOption, ProjectOptionsPersistModel>();
        CreateMap<ProjectPersistModel, ProjectViewModel>()
            .Include<GeneralProjectPersistModel, GeneralProjectViewModel>()
            .Include<CSharpProjectPersistModel, CSharpProjectViewModel>()
            .Include<CppProjectPersistModel, CppProjectViewModel>()
            .ForMember(dest => dest.Tasks, opt => opt.Ignore())
            .ForMember(dest => dest.ExtendedSystemPrompts,
                opt => opt.MapFrom(src => AutoMapModelTypeConverter.MapPrompts(src.ExtendedPrompts, promptsResource)))
            .AfterMap(AfterMapProjectViewModel);

        CreateMap<GeneralProjectPersistModel, GeneralProjectViewModel>()
            .ConstructUsing(CreateViewModel<GeneralProjectViewModel>);
        CreateMap<CSharpProjectPersistModel, CSharpProjectViewModel>()
            .ConstructUsing(CreateViewModel<CSharpProjectViewModel>);
        CreateMap<CppProjectPersistModel, CppProjectViewModel>()
            .ConstructUsing(CreateViewModel<CppProjectViewModel>);

        CreateMap<ProjectViewModel, ProjectPersistModel>()
            .Include<GeneralProjectViewModel, GeneralProjectPersistModel>()
            .Include<CSharpProjectViewModel, CSharpProjectPersistModel>()
            .Include<CppProjectViewModel, CppProjectPersistModel>()
            .ForMember(dest => dest.Client, opt => opt.MapFrom(src => src.Requester.DefaultClient))
            .ForMember(dest => dest.ExtendedPrompts,
                opt => opt.MapFrom(src => new PromptsPersistModel()
                    {
                        PromptReference = src.ExtendedSystemPrompts.Select(entry => entry.Id).ToArray(),
                    }
                ))
            .ForMember(dest => dest.UserPrompt, opt => opt.MapFrom(src => src.Requester.PromptString));
        CreateMap<GeneralProjectViewModel, GeneralProjectPersistModel>();
        CreateMap<CSharpProjectViewModel, CSharpProjectPersistModel>();
        CreateMap<CppProjectViewModel, CppProjectPersistModel>();


        CreateMap<ProjectTaskViewModel, ProjectTaskPersistModel>()
            .ForMember(dest => dest.AllowedFunctions, opt => opt.MapFrom(src => src.SelectedFunctionGroups))
            .ForMember(dest => dest.DialogItems, opt => opt.MapFrom(src => src.DialogItems));
        CreateMap<ProjectTaskPersistModel, ProjectTaskViewModel>()
            .ConstructUsing((src, ctx) =>
            {
                if (!ctx.Items.TryGetValue(AutoMapModelTypeConverter.ParentProjectViewModelKey,
                        out var parentProjectViewModel)
                    || parentProjectViewModel is not ProjectViewModel projectViewModel)
                {
                    throw new InvalidOperationException("Parent ProjectViewModel is not set in context.");
                }

                return viewModelFactory.CreateViewModel<ProjectTaskViewModel>(projectViewModel);
            })

            // 2. 排除掉需要特殊处理的集合，因为我们要手动控制它们的填充过程
            .ForMember(dest => dest.DialogItems, opt => opt.Ignore())
            .ForMember(dest => dest.SelectedFunctionGroups, opt => opt.MapFrom(src => src.AllowedFunctions))
            .AfterMap((src, dest, ctx) =>
            {
                try
                {
                    // 设置子节点需要的上下文
                    ctx.Items[AutoMapModelTypeConverter.ParentDialogViewModelKey] = dest;
                    if (src.DialogItems != null)
                    {
                        // 利用之前配置好的 IDialogPersistItem -> IDialogItem 多态映射
                        foreach (var persistItem in src.DialogItems)
                        {
                            var viewItem = ctx.Mapper.Map<IDialogItem>(persistItem);
                            dest.DialogItems.Add(viewItem);
                        }
                    }
                }
                finally
                {
                    // 清理上下文防止污染其他不相关的映射
                    ctx.Items.Remove(AutoMapModelTypeConverter.ParentDialogViewModelKey);
                }
            });
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

        // 映射 Option
        var projectOption = context.Mapper.Map<ProjectOption>(source.Option);

        // 映射 Client
        var client = source.Client == null
            ? EmptyLlmModelClient.Instance
            : context.Mapper.Map<ILLMChatClient>(source.Client);

        // 调用工厂 - 类型由泛型参数确定，无需判断
        return _viewModelFactory.CreateViewModel<TViewModel>(projectOption, client);
    }

    /// <summary>
    /// 通用的后置处理 - 所有类型共享
    /// </summary>
    private void AfterMapProjectViewModel(ProjectPersistModel source, ProjectViewModel dest, ResolutionContext context)
    {
        // 设置 Requester
        dest.Requester.PromptString = source.UserPrompt;
        // 上下文入栈
        context.Items[AutoMapModelTypeConverter.ParentProjectViewModelKey] = dest;

        try
        {
            // 映射 Tasks（带父级上下文）
            if (source.Tasks != null && source.Tasks.Length != 0)
            {
                dest.Tasks.Clear();
                var tasks = context.Mapper.Map<IEnumerable<ProjectTaskViewModel>>(source.Tasks);
                foreach (var task in tasks)
                {
                    dest.AddTask(task);
                }
            }
        }
        finally
        {
            // 清理上下文
            context.Items.Remove(AutoMapModelTypeConverter.ParentProjectViewModelKey);
        }
    }
}