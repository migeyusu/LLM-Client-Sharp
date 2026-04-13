using AutoMapper;
using LLMClient.Abstraction;
using LLMClient.Configuration;
using LLMClient.ContextEngineering.Tools;


using LLMClient.Persistence;
using LLMClient.Project;
using LLMClient.ToolCall;
using LLMClient.ToolCall.DefaultPlugins;
using LLMClient.ToolCall.MCP;

namespace LLMClient;

public class FunctionGroupPersistenceProfile : Profile
{
    public FunctionGroupPersistenceProfile(IMcpServiceCollection mcpServiceCollection)
    {
        CreateMap<VariableItem, VariableItem>();
        CreateMap<ProxyOption, ProxyOption>();
        CreateMap<ProxySetting, ProxySetting>();

        CreateMap<IAIFunctionGroup, AIFunctionGroupDefinitionPersistModel>()
            .Include<StdIOServerItem, StdIOServerItemPersistModel>()
            .Include<SseServerItem, SseServerItemPersistModel>()
            .Include<FileSystemPlugin, FileSystemPluginPersistModel>()
            .Include<WslCLIPlugin, WslCliPluginPersistModel>()
            .Include<WinCLIPlugin, WinCliPluginPersistModel>()
            .Include<GoogleSearchPlugin, GoogleSearchPluginPersistModel>()
            .Include<UrlFetcherPlugin, UrlFetcherPluginPersistModel>()
            .Include<ProjectAwarenessPlugin, ProjectAwarenessPluginPersistModel>()
            .Include<SymbolSemanticPlugin, SymbolSemanticPluginPersistModel>()
            .Include<CodeSearchPlugin, CodeSearchPluginPersistModel>()
            .Include<CodeReadingPlugin, CodeReadingPluginPersistModel>();

        CreateMap<AIFunctionGroupDefinitionPersistModel, IAIFunctionGroup>()
            .Include<StdIOServerItemPersistModel, IAIFunctionGroup>()
            .Include<SseServerItemPersistModel, IAIFunctionGroup>()
            .Include<FileSystemPluginPersistModel, IAIFunctionGroup>()
            .Include<WslCliPluginPersistModel, IAIFunctionGroup>()
            .Include<WinCliPluginPersistModel, IAIFunctionGroup>()
            .Include<GoogleSearchPluginPersistModel, IAIFunctionGroup>()
            .Include<UrlFetcherPluginPersistModel, IAIFunctionGroup>()
            .Include<ProjectAwarenessPluginPersistModel, IAIFunctionGroup>()
            .Include<SymbolSemanticPluginPersistModel, IAIFunctionGroup>()
            .Include<CodeSearchPluginPersistModel, IAIFunctionGroup>()
            .Include<CodeReadingPluginPersistModel, IAIFunctionGroup>();

        CreateMap<McpServerItem, McpServerItemPersistModel>()
            .Include<StdIOServerItem, StdIOServerItemPersistModel>()
            .Include<SseServerItem, SseServerItemPersistModel>()
            .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Name))
            .ForMember(dest => dest.ProjectUrl, opt => opt.MapFrom(src => src.ProjectUrl))
            .ForMember(dest => dest.UserPrompt, opt => opt.MapFrom(src => src.UserPrompt))
            .ForMember(dest => dest.IsEnabled, opt => opt.MapFrom(src => src.IsEnabled));
        CreateMap<McpServerItemPersistModel, McpServerItem>()
            .Include<StdIOServerItemPersistModel, StdIOServerItem>()
            .Include<SseServerItemPersistModel, SseServerItem>()
            .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Name))
            .ForMember(dest => dest.ProjectUrl, opt => opt.MapFrom(src => src.ProjectUrl))
            .ForMember(dest => dest.UserPrompt, opt => opt.MapFrom(src => src.UserPrompt))
            .ForMember(dest => dest.IsEnabled, opt => opt.MapFrom(src => src.IsEnabled));

        CreateMap<StdIOServerItem, StdIOServerItemPersistModel>()
            .IncludeBase<McpServerItem, McpServerItemPersistModel>();
        CreateMap<StdIOServerItemPersistModel, StdIOServerItem>()
            .IncludeBase<McpServerItemPersistModel, McpServerItem>();
        CreateMap<SseServerItem, SseServerItemPersistModel>()
            .IncludeBase<McpServerItem, McpServerItemPersistModel>();
        CreateMap<SseServerItemPersistModel, SseServerItem>()
            .IncludeBase<McpServerItemPersistModel, McpServerItem>();

        CreateMap<StdIOServerItemPersistModel, IAIFunctionGroup>()
            .ConstructUsing((src, ctx) => mcpServiceCollection.TryGet(ctx.Mapper.Map<StdIOServerItem>(src)));
        CreateMap<SseServerItemPersistModel, IAIFunctionGroup>()
            .ConstructUsing((src, ctx) => mcpServiceCollection.TryGet(ctx.Mapper.Map<SseServerItem>(src)));

        CreateMap<FileSystemPlugin, FileSystemPluginPersistModel>();
        CreateMap<FileSystemPluginPersistModel, FileSystemPlugin>()
            .ConstructUsing(_ => new FileSystemPlugin())
            .ForMember(dest => dest.BypassPaths,
                opt => opt.MapFrom(src => src.BypassPaths ?? Array.Empty<string>()));
        CreateMap<FileSystemPluginPersistModel, IAIFunctionGroup>()
            .ConstructUsing((src, ctx) => ctx.Mapper.Map<FileSystemPlugin>(src));

        CreateMap<WslCLIPlugin, WslCliPluginPersistModel>();
        CreateMap<WslCliPluginPersistModel, WslCLIPlugin>()
            .ConstructUsing(src => new WslCLIPlugin(
                src.VerifyRequiredCommands != null
                    ? src.VerifyRequiredCommands
                    : WslCLIPlugin.DefaultDeniedCommands));
        CreateMap<WslCliPluginPersistModel, IAIFunctionGroup>()
            .ConstructUsing((src, ctx) => ctx.Mapper.Map<WslCLIPlugin>(src));

        CreateMap<WinCLIPlugin, WinCliPluginPersistModel>();
        CreateMap<WinCliPluginPersistModel, WinCLIPlugin>()
            .ConstructUsing(src => new WinCLIPlugin(
                src.VerifyRequiredCommands != null
                    ? src.VerifyRequiredCommands
                    : WinCLIPlugin.DefaultDeniedCommands));
        CreateMap<WinCliPluginPersistModel, IAIFunctionGroup>()
            .ConstructUsing((src, ctx) => ctx.Mapper.Map<WinCLIPlugin>(src));

        CreateMap<GoogleSearchPlugin, GoogleSearchPluginPersistModel>();
        CreateMap<GoogleSearchPluginPersistModel, GoogleSearchPlugin>()
            .ConstructUsing(_ => new GoogleSearchPlugin());
        CreateMap<GoogleSearchPluginPersistModel, IAIFunctionGroup>()
            .ConstructUsing((src, ctx) => ctx.Mapper.Map<GoogleSearchPlugin>(src));

        CreateMap<UrlFetcherPlugin, UrlFetcherPluginPersistModel>();
        CreateMap<UrlFetcherPluginPersistModel, UrlFetcherPlugin>()
            .ConstructUsing(_ => new UrlFetcherPlugin());
        CreateMap<UrlFetcherPluginPersistModel, IAIFunctionGroup>()
            .ConstructUsing((src, ctx) => ctx.Mapper.Map<UrlFetcherPlugin>(src));

        CreateMap<ProjectAwarenessPlugin, ProjectAwarenessPluginPersistModel>();
        CreateMap<ProjectAwarenessPluginPersistModel, IAIFunctionGroup>()
            .ConstructUsing((src, ctx) => ResolveProjectScopedFunctionGroup(src, ctx));
        CreateMap<SymbolSemanticPlugin, SymbolSemanticPluginPersistModel>();
        CreateMap<SymbolSemanticPluginPersistModel, IAIFunctionGroup>()
            .ConstructUsing((src, ctx) => ResolveProjectScopedFunctionGroup(src, ctx));
        CreateMap<CodeSearchPlugin, CodeSearchPluginPersistModel>();
        CreateMap<CodeSearchPluginPersistModel, IAIFunctionGroup>()
            .ConstructUsing((src, ctx) => ResolveProjectScopedFunctionGroup(src, ctx));
        CreateMap<CodeReadingPlugin, CodeReadingPluginPersistModel>();
        CreateMap<CodeReadingPluginPersistModel, IAIFunctionGroup>()
            .ConstructUsing((src, ctx) => ResolveProjectScopedFunctionGroup(src, ctx));

        CreateMap<CheckableFunctionGroupTree, AIFunctionGroupPersistObject>()
            .ConvertUsing<AutoMapModelTypeConverter>();
        CreateMap<AIFunctionGroupPersistObject, CheckableFunctionGroupTree>()
            .ConvertUsing<AutoMapModelTypeConverter>();

        CreateMap<AIFunctionGroupPersistObject, IAIFunctionGroup>()
            .As<CheckableFunctionGroupTree>();
        CreateMap<IAIFunctionGroup, AIFunctionGroupPersistObject>()
            .Include<CheckableFunctionGroupTree, AIFunctionGroupPersistObject>();
    }

    private static IAIFunctionGroup ResolveProjectScopedFunctionGroup(
        AIFunctionGroupDefinitionPersistModel persistModel, ResolutionContext context)
    {
        ProjectViewModel? projectViewModel = null;
        if (context.Items.TryGetValue(AutoMapModelTypeConverter.ParentProjectViewModelKey, out var project)
            && project is ProjectViewModel directProjectViewModel)
        {
            projectViewModel = directProjectViewModel;
        }
        else if (context.Items.TryGetValue(AutoMapModelTypeConverter.ParentDialogViewModelKey, out var dialog)
                 && dialog is ProjectSessionViewModel projectSessionViewModel)
        {
            projectViewModel = projectSessionViewModel.ParentProject;
        }

        if (projectViewModel == null)
        {
            throw new InvalidOperationException(
                $"Project context is required to restore function group '{persistModel.GetType().Name}'.");
        }

        if (!projectViewModel.TryResolvePersistedFunctionGroup(persistModel, out var functionGroup)
            || functionGroup == null)
        {
            throw new InvalidOperationException(
                $"Project '{projectViewModel.GetType().Name}' cannot restore function group '{persistModel.GetType().Name}'.");
        }

        return functionGroup;
    }
}

