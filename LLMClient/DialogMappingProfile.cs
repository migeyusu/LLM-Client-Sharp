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
using System.Text.Json;

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
                opt => opt.MapFrom(src => ExtractTokensCounterToPo(src.AdditionalProperties)));
        CreateMap<ChatMessagePO, ChatMessage>()
            .ForMember(dest => dest.AdditionalProperties,
                opt => opt.MapFrom(src => RestoreAdditionalPropertiesFromPo(src.AdditionalProperties)));
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

    /// <summary>
    /// 从 ChatMessage.AdditionalProperties 中只提取 TokensCounter 键，
    /// 以 Dictionary&lt;string, object?&gt; 写入 ChatMessagePO，防止不可序列化对象污染。
    /// </summary>
    private static Dictionary<string, object?>? ExtractTokensCounterToPo(AdditionalPropertiesDictionary? props)
    {
        if (props == null ||
            !props.TryGetValue(CoreExtension.TokensCounterKey, out var tokenValue) ||
            tokenValue == null)
        {
            return null;
        }

        return new Dictionary<string, object?>
        {
            [CoreExtension.TokensCounterKey] = Convert.ToInt64(tokenValue)
        };
    }

    /// <summary>
    /// 将 ChatMessagePO.AdditionalProperties 中的条目还原回
    /// ChatMessage.AdditionalProperties（AdditionalPropertiesDictionary）。
    /// JSON 反序列化时 object 值以 JsonElement 形式到达，需要拆箱为基元类型。
    /// </summary>
    private static AdditionalPropertiesDictionary? RestoreAdditionalPropertiesFromPo(
        Dictionary<string, object?>? props)
    {
        if (props == null || props.Count == 0)
        {
            return null;
        }

        var result = new AdditionalPropertiesDictionary();
        foreach (var (key, rawValue) in props)
        {
            result[key] = rawValue is JsonElement element ? UnboxJsonElement(element) : rawValue;
        }

        return result;
    }

    /// <summary>
    /// 将 System.Text.Json 反序列化产生的 JsonElement 转换为对应的 CLR 基元类型。
    /// </summary>
    private static object? UnboxJsonElement(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Number when element.TryGetInt64(out var l) => l,
        JsonValueKind.Number when element.TryGetDouble(out var d) => d,
        JsonValueKind.String => element.GetString(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Null => null,
        _ => element.GetRawText()
    };
}
