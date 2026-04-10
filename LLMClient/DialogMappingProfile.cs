using AutoMapper;
using LLMClient.Abstraction;
using LLMClient.Agent;
using LLMClient.Agent.Inspector;
using LLMClient.Agent.MiniSWE;
using LLMClient.Agent.Planner;
using LLMClient.Component.ViewModel;
using LLMClient.Configuration;

using LLMClient.Dialog;
using LLMClient.Endpoints;
using LLMClient.Endpoints.OpenAIAPI;
using LLMClient.Persistance;
using LLMClient.Workflow.Research;
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