using AutoMapper;
using LLMClient.Abstraction;
using LLMClient.Agent;
using LLMClient.Agent.Inspector;
using LLMClient.Agent.MiniSWE;
using LLMClient.Agent.Planner;
using LLMClient.Agent.Research;
using LLMClient.Component.ViewModel;
using LLMClient.Configuration;

using LLMClient.Dialog;
using LLMClient.Endpoints;
using LLMClient.Endpoints.OpenAIAPI;

using LLMClient.Persistence;
using Microsoft.Extensions.AI;

namespace LLMClient;

public class DialogMappingProfile : Profile
{
    public DialogMappingProfile()
    {
        CreateMap<IThinkingConfig, GeekAIThinkingConfig>();
        CreateMap<IThinkingConfig, OpenRouterReasoningConfig>();
        CreateMap<IThinkingConfig, NVDAAPIThinkingConfig>();

        CreateMap<IAgent, AgentPersistModel>().IncludeAllDerived();
        CreateMap<AgentPersistModel, IAgent>().IncludeAllDerived();
        
        CreateMap<MiniSweAgent, MiniSweAgentPersistModel>();
        CreateMap<MiniSweAgentPersistModel, MiniSweAgent>()
            .ConstructUsing((src, ctx) =>
            {
                var client = src.ChatClient != null
                    ? ctx.Mapper.Map<ILLMChatClient>(src.ChatClient)
                    : EmptyLlmModelClient.Instance;
                return new MiniSweAgent(client, src.AgentOption ?? new AgentOption());
            });

        CreateMap<InspectAgent, InspectAgentPersistModel>();
        CreateMap<InspectAgentPersistModel, InspectAgent>()
            .ConstructUsing((src, ctx) =>
            {
                var client = src.ChatClient != null
                    ? ctx.Mapper.Map<ILLMChatClient>(src.ChatClient)
                    : EmptyLlmModelClient.Instance;
                return new InspectAgent(client, src.AgentOption ?? new AgentOption());
            });

        CreateMap<PlannerAgent, PlannerAgentPersistModel>();
        CreateMap<PlannerAgentPersistModel, PlannerAgent>()
            .ConstructUsing((src, ctx) =>
            {
                var client = src.ChatClient != null
                    ? ctx.Mapper.Map<ILLMChatClient>(src.ChatClient)
                    : EmptyLlmModelClient.Instance;
                return new PlannerAgent(client, src.AgentOption ?? new AgentOption());
            });

        CreateMap<SummaryAgent, SummaryAgentPersistModel>();
        CreateMap<SummaryAgentPersistModel, SummaryAgent>()
            .ConstructUsing((src, ctx) =>
            {
                var client = src.ChatClient != null
                    ? ctx.Mapper.Map<ILLMChatClient>(src.ChatClient)
                    : EmptyLlmModelClient.Instance;
                return new SummaryAgent(client);
            });
        
        CreateMap<NvidiaResearchClient, NvidiaResearchClientPersistModel>();
        // Note: NvidiaResearchClientPersistModel -> NvidiaResearchClient mapping 
        // requires GlobalOptions and should be handled by NvidiaResearchClientFactory
        // instead of AutoMapper to avoid ServiceLocator anti-pattern

        CreateMap<IAIContent, AIContent>().IncludeAllDerived();
        CreateMap<AIContent, IAIContent>().IncludeAllDerived();
        CreateMap<ChatMessage, ChatMessagePO>()
            .ForMember(dest => dest.AdditionalProperties,
                opt => opt.MapFrom<ChatMessageToPoAdditionalPropertiesResolver>());
        CreateMap<ChatMessagePO, ChatMessage>()
            .ForMember(dest => dest.AdditionalProperties,
                opt => opt.MapFrom<PoToChatMessageAdditionalPropertiesResolver>());
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
            .ConstructUsing((po, _) => new FunctionResultContent(po.CallId, po.Result));
        CreateMap<TextReasoningContent, TextReasoningContentPO>();
        CreateMap<TextReasoningContentPO, TextReasoningContent>();
        CreateMap<UriContent, UriContentPO>();
        CreateMap<UriContentPO, UriContent>()
            .ConstructUsing(po => new UriContent(po.Uri!, po.MediaType!));
        CreateMap<UsageContent, UsageContentPO>();
        CreateMap<UsageContentPO, UsageContent>();

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
    }
}

/// <summary>
/// 从 ChatMessage.AdditionalProperties 中只提取 TokensCounter 键，
/// 以 Dictionary&lt;string, long&gt; 写入 ChatMessagePO，防止不可序列化对象污染。
/// </summary>
file sealed class ChatMessageToPoAdditionalPropertiesResolver
    : IValueResolver<ChatMessage, ChatMessagePO, Dictionary<string, long>?>
{
    public Dictionary<string, long>? Resolve(
        ChatMessage source, ChatMessagePO destination,
        Dictionary<string, long>? destMember, ResolutionContext context)
    {
        if (source.AdditionalProperties == null ||
            !source.AdditionalProperties.TryGetValue(CoreExtension.TokensCounterKey, out var tokenValue) ||
            tokenValue == null)
        {
            return null;
        }

        return new Dictionary<string, long>
        {
            [CoreExtension.TokensCounterKey] = Convert.ToInt64(tokenValue)
        };
    }
}

/// <summary>
/// 将 ChatMessagePO.AdditionalProperties 中的 TokensCounter 还原回
/// ChatMessage.AdditionalProperties（AdditionalPropertiesDictionary）。
/// </summary>
file sealed class PoToChatMessageAdditionalPropertiesResolver
    : IValueResolver<ChatMessagePO, ChatMessage, AdditionalPropertiesDictionary?>
{
    public AdditionalPropertiesDictionary? Resolve(
        ChatMessagePO source, ChatMessage destination,
        AdditionalPropertiesDictionary? destMember, ResolutionContext context)
    {
        if (source.AdditionalProperties == null ||
            !source.AdditionalProperties.TryGetValue(CoreExtension.TokensCounterKey, out var tokenValue))
        {
            return null;
        }

        var result = new AdditionalPropertiesDictionary();
        result[CoreExtension.TokensCounterKey] = tokenValue;
        return result;
    }
}
