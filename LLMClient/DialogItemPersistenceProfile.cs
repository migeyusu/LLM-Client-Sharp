using AutoMapper;
using LLMClient.Abstraction;
using LLMClient.Agent;

using LLMClient.Dialog;
using LLMClient.Dialog.Models;
using LLMClient.Endpoints;
using LLMClient.Persistance;

namespace LLMClient;

public class DialogItemPersistenceProfile : Profile
{
    public DialogItemPersistenceProfile()
    {
        const string parentDialogViewModelKey = AutoMapModelTypeConverter.ParentDialogViewModelKey;

        CreateMap<IDialogItem, IDialogPersistItem>()
            .Include<RequestViewItem, RequestPersistItem>()
            .Include<EraseViewItem, ErasePersistItem>()
            .Include<ParallelResponseViewItem, ParallelResponsePersisItem>()
            .Include<LinearResponseViewItem, LinearHistoryResponsePersistItem>();

        CreateMap<RequestViewItem, RequestPersistItem>()
            .IncludeBase<IDialogItem, IDialogPersistItem>()
            .ForMember(dest => dest.FunctionGroups, opt => opt.MapFrom(src => src.FunctionGroups))
            .ForMember(dest => dest.SearchService, opt => opt.MapFrom(src => src.SearchOption));
        CreateMap<EraseViewItem, ErasePersistItem>()
            .IncludeBase<IDialogItem, IDialogPersistItem>();
        CreateMap<ParallelResponseViewItem, ParallelResponsePersisItem>()
            .IncludeBase<IDialogItem, IDialogPersistItem>()
            .ForMember(item => item.ResponseItems, opt => opt.MapFrom(item => item.Items));
        CreateMap<LinearResponseViewItem, LinearHistoryResponsePersistItem>()
            .IncludeBase<IDialogItem, IDialogPersistItem>()
            .ForMember(dest => dest.Response, opt => opt.MapFrom(src => src.Response))
            .ForMember(dest => dest.Items, opt => opt.Ignore());

        CreateMap<IDialogPersistItem, IDialogItem>()
            .Include<RequestPersistItem, RequestViewItem>()
            .Include<ErasePersistItem, EraseViewItem>()
            .Include<ParallelResponsePersisItem, ParallelResponseViewItem>()
            .Include<LinearHistoryResponsePersistItem, LinearResponseViewItem>();

        CreateMap<RequestPersistItem, RequestViewItem>()
            .ConstructUsing((item, context) =>
            {
                var contextItems = context.Items;
                if (!contextItems.TryGetValue(parentDialogViewModelKey, out var parentDialogViewModel)
                    || parentDialogViewModel is not DialogSessionViewModel parentViewModel)
                {
                    throw new InvalidOperationException("Parent DialogViewModel is not set in context.");
                }

                return new RequestViewItem(item.RawTextMessage ?? string.Empty, parentViewModel);
            })
            .ForMember(dest => dest.FunctionGroups, opt => opt.MapFrom(src => src.FunctionGroups))
            .ForMember(dest => dest.SearchOption, opt => opt.MapFrom(src => src.SearchService));
        CreateMap<ErasePersistItem, EraseViewItem>()
            .IncludeBase<IDialogPersistItem, IDialogItem>();
        CreateMap<ParallelResponsePersisItem, ParallelResponseViewItem>()
            .IncludeBase<IDialogPersistItem, IDialogItem>()
            .ConstructUsing((_, context) =>
            {
                var contextItems = context.Items;
                if (!contextItems.TryGetValue(parentDialogViewModelKey, out var parentDialogViewModel)
                    || parentDialogViewModel is not DialogSessionViewModel parentViewModel)
                {
                    throw new InvalidOperationException("Parent DialogViewModel is not set in context.");
                }

                return new ParallelResponseViewItem(parentViewModel);
            })
            .AfterMap((source, destination, context) =>
            {
                var items = source.ResponseItems.Select(x =>
                    context.Mapper.Map<ClientResponsePersistItem, ClientResponseViewItem>(x)).ToArray();
                foreach (var item in items)
                {
                    destination.Items.Add(item);
                }
            });
        CreateMap<LinearHistoryResponsePersistItem, LinearResponseViewItem>()
            .IncludeBase<IDialogPersistItem, IDialogItem>()
            .ConstructUsing((source, context) =>
            {
                var contextItems = context.Items;
                if (!contextItems.TryGetValue(parentDialogViewModelKey, out var parentDialogViewModel)
                    || parentDialogViewModel is not DialogSessionViewModel parentViewModel)
                {
                    throw new InvalidOperationException("Parent DialogViewModel is not set in context.");
                }

                var agent = source.Agent != null
                    ? context.Mapper.Map<IAgent>(source.Agent)
                    : null;
                var persistedResponse = source.Response ?? source.Items?.FirstOrDefault();
                var response = persistedResponse != null
                    ? context.Mapper.Map<RawResponsePersistItem, RawResponseViewItem>(persistedResponse)
                    : new RawResponseViewItem();
                return new LinearResponseViewItem(parentViewModel, agent, response);
            });

        CreateMap<IResponse, ResponseViewItemBase>()
            .Include<IResponse, ClientResponseViewItem>();
        CreateMap<IResponse, RawResponseViewItem>()
            .IncludeBase<IResponse, ResponseViewItemBase>();
        CreateMap<IResponse, ClientResponseViewItem>()
            .IncludeBase<IResponse, ResponseViewItemBase>();
        CreateMap<AgentTaskResult, ResponseViewItemBase>()
            .IncludeBase<IResponse, ResponseViewItemBase>();
        CreateMap<AgentTaskResult, RawResponseViewItem>()
            .IncludeBase<IResponse, RawResponseViewItem>();
        CreateMap<AgentTaskResult, ClientResponseViewItem>()
            .IncludeBase<IResponse, ClientResponseViewItem>();

        CreateMap<ResponseViewItemBase, ResponsePersistItemBase>()
            .Include<ClientResponseViewItem, ClientResponsePersistItem>()
            .Include<RawResponseViewItem, RawResponsePersistItem>();
        CreateMap<ClientResponseViewItem, ClientResponsePersistItem>()
            .IncludeBase<ResponseViewItemBase, ResponsePersistItemBase>()
            .PreserveReferences();
        CreateMap<RawResponseViewItem, RawResponsePersistItem>()
            .IncludeBase<ResponseViewItemBase, ResponsePersistItemBase>()
            .PreserveReferences();

        CreateMap<ResponsePersistItemBase, ResponseViewItemBase>()
            .Include<ClientResponsePersistItem, ClientResponseViewItem>()
            .Include<RawResponsePersistItem, RawResponseViewItem>();
        CreateMap<ClientResponsePersistItem, ClientResponseViewItem>()
            .IncludeBase<ResponsePersistItemBase, ResponseViewItemBase>()
            .PreserveReferences()
            .ConstructUsing((source, context) =>
            {
                ILLMChatClient llmClient = EmptyLlmModelClient.Instance;
                if (source.Client != null)
                {
                    llmClient = context.Mapper.Map<ParameterizedLLMModelPO, ILLMChatClient>(source.Client);
                }

                return new ClientResponseViewItem(llmClient);
            });
        CreateMap<RawResponsePersistItem, RawResponseViewItem>()
            .IncludeBase<ResponsePersistItemBase, ResponseViewItemBase>()
            .PreserveReferences();
    }
}

