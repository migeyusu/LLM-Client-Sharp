using System.Collections.Immutable;
using AutoMapper;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using LLMClient.ContextEngineering.Tools.Models;

namespace LLMClient.ContextEngineering.Analysis;

/// <summary>
/// 优化版 Roslyn 映射配置：充分利用继承 + 两步映射策略
/// </summary>
public class RoslynMappingProfile : Profile
{
    public RoslynMappingProfile()
    {
        CreateMap<IParameterSymbol, ParameterInfo>()
            .ForMember(d => d.Type,
                o => o.MapFrom(s => s.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)))
            .ForMember(d => d.HasDefaultValue, o => o.MapFrom(s => s.HasExplicitDefaultValue))
            .ForMember(d => d.DefaultValue,
                o => o.MapFrom(s => s.ExplicitDefaultValue != null ? s.ExplicitDefaultValue.ToString() : null));

        CreateMap<ISymbol, SymbolInfo>()
            .Include<IMethodSymbol, MemberInfo>()
            .Include<IPropertySymbol, MemberInfo>()
            .Include<IFieldSymbol, MemberInfo>()
            .Include<IEventSymbol, MemberInfo>()
            .Include<INamedTypeSymbol, TypeInfo>()
            .ForMember(d => d.UniqueId, o => o.MapFrom(s => s.GetDocumentationCommentId()))
            .ForMember(d => d.Signature, o => o.MapFrom(s => s.ToDisplayString(null)))
            .ForMember(d => d.Kind, o => o.MapFrom(s => s.Kind.ToString()))
            .ForMember(d => d.Accessibility, o => o.MapFrom(s => s.DeclaredAccessibility.ToString()))
            .ForMember(d => d.Locations, o => o.MapFrom(s => s.GetLocations()))
            .ForMember(d => d.Attributes, o => o.MapFrom((symbol => symbol.ExtractAttributes())))
            .ForMember(d => d.Summary, o => o.Ignore());

        CreateMap<IMethodSymbol, MemberInfo>()
            .IncludeBase<ISymbol, SymbolInfo>()
            .ForMember(d => d.ReturnType, o => o.MapFrom(s => FormatTypeName(s.ReturnType)))
            .ForMember(dest => dest.Parameters,
                opt => opt.MapFrom(src => MapParameters(src.Parameters)));

        CreateMap<IPropertySymbol, MemberInfo>()
            .IncludeBase<ISymbol, SymbolInfo>()
            .ForMember(d => d.IsAsync, o => o.MapFrom(s => false))
            .ForMember(d => d.ReturnType, o => o.MapFrom(s => FormatTypeName(s.Type)))
            .ForMember(dest => dest.Parameters,
                opt => opt.MapFrom(src => MapParameters(src.Parameters)));

        CreateMap<IFieldSymbol, MemberInfo>()
            .IncludeBase<ISymbol, SymbolInfo>()
            .ForMember(d => d.IsAsync, o => o.MapFrom(s => false))
            .ForMember(d => d.IsVirtual, o => o.MapFrom(s => false))
            .ForMember(d => d.IsOverride, o => o.MapFrom(s => false))
            .ForMember(d => d.ReturnType, o => o.MapFrom(s => FormatTypeName(s.Type)))
            .ForMember(d => d.Parameters, o => o.Ignore());

        CreateMap<IEventSymbol, MemberInfo>()
            .IncludeBase<ISymbol, SymbolInfo>()
            .ForMember(d => d.IsAsync, o => o.MapFrom(s => false))
            .ForMember(d => d.ReturnType, o => o.MapFrom(s => FormatTypeName(s.Type)))
            .ForMember(d => d.Parameters, o => o.Ignore());

        CreateMap<INamedTypeSymbol, TypeInfo>()
            .IncludeBase<ISymbol, SymbolInfo>()
            .ForMember(d => d.IsPartial, o => o.Ignore()) // 只能从 Syntax 获取
            .ForMember(d => d.BaseTypes, o => o.MapFrom(s =>
                s.BaseType != null && s.BaseType.SpecialType == SpecialType.None
                    ? new List<string> { FormatTypeName(s.BaseType) }
                    : new List<string>()))
            .ForMember(d => d.ImplementedInterfaces, o => o.MapFrom(s =>
                s.Interfaces.Select(FormatTypeName).ToList()))
            .ForMember(d => d.Members, o => o.Ignore());

        // 基础映射：MemberDeclarationSyntax → MemberInfo
        CreateMap<MemberDeclarationSyntax, SymbolInfo?>()
            .Include<MethodDeclarationSyntax, MemberInfo?>()
            .Include<PropertyDeclarationSyntax, MemberInfo?>()
            .Include<ConstructorDeclarationSyntax, MemberInfo?>()
            .Include<EventDeclarationSyntax, MemberInfo?>()
            .Include<TypeDeclarationSyntax, TypeInfo?>()
            .Include<FieldDeclarationSyntax, MemberInfo?>()
            .Include<EventFieldDeclarationSyntax, MemberInfo?>() 
            .Include<OperatorDeclarationSyntax, MemberInfo?>() 
            .Include<IndexerDeclarationSyntax, MemberInfo?>() 
            .Include<ConversionOperatorDeclarationSyntax, MemberInfo?>()
            .Include<DestructorDeclarationSyntax, MemberInfo?>()
            .ConvertUsing((_, _, _) => null);
        

        // 子类映射：继承基础映射，只需声明即可
        CreateMap<MethodDeclarationSyntax, MemberInfo?>()
            .IncludeBase<MemberDeclarationSyntax, SymbolInfo?>()
            .AfterMap((src, dest, _) => ApplySyntaxInfo(src, dest))
            .ConvertUsing((src, _, ctx) =>
                ConvertViaSymbol<MethodDeclarationSyntax, MemberInfo?>(src, ctx,
                    (m, node) => m.GetDeclaredSymbol(node)));

        CreateMap<PropertyDeclarationSyntax, MemberInfo?>()
            .IncludeBase<MemberDeclarationSyntax, SymbolInfo?>()
            .AfterMap((src, dest, _) => ApplySyntaxInfo(src, dest))
            .ConvertUsing((src, _, ctx) =>
                ConvertViaSymbol<PropertyDeclarationSyntax, MemberInfo?>(src, ctx,
                    (m, node) => m.GetDeclaredSymbol(node)));

        CreateMap<ConstructorDeclarationSyntax, MemberInfo?>()
            .IncludeBase<MemberDeclarationSyntax, SymbolInfo?>()
            .AfterMap((src, dest, _) => ApplySyntaxInfo(src, dest))
            .ConvertUsing((src, _, ctx) =>
                ConvertViaSymbol<ConstructorDeclarationSyntax, MemberInfo?>(src, ctx,
                    (m, node) => m.GetDeclaredSymbol(node)));

        CreateMap<EventDeclarationSyntax, MemberInfo?>()
            .IncludeBase<MemberDeclarationSyntax, SymbolInfo?>()
            .AfterMap((src, dest, _) => ApplySyntaxInfo(src, dest))
            .ConvertUsing((src, _, ctx) =>
                ConvertViaSymbol<EventDeclarationSyntax, MemberInfo?>(src, ctx,
                    (m, node) => m.GetDeclaredSymbol(node)));
        
        CreateMap<DestructorDeclarationSyntax, MemberInfo?>()
            .AfterMap((src, dest, _) => ApplySyntaxInfo(src, dest))
            .ConvertUsing((src, _, ctx) => ConvertViaSymbol<DestructorDeclarationSyntax, MemberInfo?>(
                src, ctx, (m, node) => m.GetDeclaredSymbol(node)));

        // FieldDeclarationSyntax 特殊处理（一个声明可以包含多个变量）
        CreateMap<FieldDeclarationSyntax, MemberInfo?>()
            .IncludeBase<MemberDeclarationSyntax, SymbolInfo?>()
            .AfterMap((src, dest, _) => ApplySyntaxInfo(src, dest))
            .ConvertUsing((src, _, context) =>
            {
                var variable = src.Declaration.Variables.FirstOrDefault();
                if (variable == null) return null;
                var semanticModel = GetSemanticModel(context);
                if (semanticModel?.GetDeclaredSymbol(variable) is not IFieldSymbol symbol) return null;
                var memberInfo = context.Mapper.Map<MemberInfo?>(symbol);
                return memberInfo;
            });

        CreateMap<OperatorDeclarationSyntax, MemberInfo?>()
            .AfterMap((src, dest, _) => ApplySyntaxInfo(src, dest))
            .ConvertUsing((src, _, ctx) => ConvertViaSymbol<OperatorDeclarationSyntax, MemberInfo?>(
                src, ctx, (m, node) => m.GetDeclaredSymbol(node)));
        
        CreateMap<IndexerDeclarationSyntax, MemberInfo?>()
            .AfterMap((src, dest, _) => ApplySyntaxInfo(src, dest))
            .ConvertUsing((src, _, ctx) => ConvertViaSymbol<IndexerDeclarationSyntax, MemberInfo?>(
                src, ctx, (m, node) => m.GetDeclaredSymbol(node)));
        
        CreateMap<ConversionOperatorDeclarationSyntax, MemberInfo?>()
            .AfterMap((src, dest, _) => ApplySyntaxInfo(src, dest))
            .ConvertUsing((src, _, ctx) => ConvertViaSymbol<ConversionOperatorDeclarationSyntax, MemberInfo?>(
                src, ctx, (m, node) => m.GetDeclaredSymbol(node)));
        
        CreateMap<EventFieldDeclarationSyntax, MemberInfo?>()
            .AfterMap((src, dest, _) => ApplySyntaxInfo(src, dest))
            .ConvertUsing((src, _, ctx) => ConvertViaSymbol<EventFieldDeclarationSyntax, MemberInfo?>(
                src, ctx, (model, node) =>
                {
                    // EventField 和 Field 一样，可能一行声明多个 (public event Action A, B;)
                    // 我们取第一个变量来获取符号
                    var variable = node.Declaration.Variables.FirstOrDefault();
                    return variable == null ? null : model.GetDeclaredSymbol(variable);
                }));
        
        CreateMap<TypeDeclarationSyntax, TypeInfo?>()
            .IncludeBase<MemberDeclarationSyntax, SymbolInfo?>()
            .AfterMap((src, dest, _) =>
            {
                if (dest == null) return;
                ApplySyntaxInfo(src, dest);
                dest.IsPartial = src.Modifiers.Any(SyntaxKind.PartialKeyword);
            })
            .ConvertUsing((src, _, ctx) =>
                ConvertViaSymbol<TypeDeclarationSyntax, TypeInfo?>(src, ctx, (m, node) => m.GetDeclaredSymbol(node)));

        // ── View Mappings ──
        CreateMap<CodeLocation, LocationView>()
            .ForMember(d => d.StartLine, o => o.MapFrom(s => s.Location.Start.Line + 1))
            .ForMember(d => d.EndLine, o => o.MapFrom(s => s.Location.End.Line + 1));

        CreateMap<Location, LocationView>()
            .ConvertUsing(loc => MapLocation(loc));

        CreateMap<ISymbol, SymbolBriefView>()
            .ForMember(d => d.SymbolId, o => o.MapFrom(s => s.GetSymbolId()))
            .ForMember(d => d.Signature,
                o => o.MapFrom(s => s.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)))
            .ForMember(d => d.Location, o => o.MapFrom(s => s.Locations.FirstOrDefault(l => l.IsInSource)));

        CreateMap<SymbolInfo, SymbolBriefView>()
            .ForMember(d => d.Location, o => o.MapFrom(s => s.Locations.FirstOrDefault()));

        CreateMap<ParameterInfo, ParameterView>();

        CreateMap<SymbolInfo, SymbolDetailView>()
            .Include<TypeInfo, SymbolDetailView>()
            .Include<MemberInfo, SymbolDetailView>()
            .ForMember(d => d.TypeDetail, o => o.Ignore())
            .ForMember(d => d.MemberDetail, o => o.Ignore());

        CreateMap<TypeInfo, SymbolDetailView>()
            .ForMember(d => d.TypeDetail, o => o.MapFrom(s => s));

        CreateMap<MemberInfo, SymbolDetailView>()
            .ForMember(d => d.MemberDetail, o => o.MapFrom(s => s));

        CreateMap<TypeInfo, TypeDetailExtra>()
            .ForMember(d => d.MemberCount, o => o.MapFrom(s => s.Members.Count));

        CreateMap<MemberInfo, MemberDetailExtra>()
            .ForMember(d => d.ContainingType, o => o.MapFrom((_, _, _, context) =>
                context.Items.TryGetValue("ContainingType", out var val) ? val : null));

        CreateMap<TypeInfo, TypeSummaryView>()
            .ForMember(d => d.MemberCount, o => o.MapFrom(s => s.Members.Count))
            .ForMember(d => d.Location, o => o.MapFrom(s => s.Locations.FirstOrDefault()));

        CreateMap<MemberInfo, MemberSummaryView>()
            .ForMember(d => d.Location, o => o.MapFrom(s => s.Locations.FirstOrDefault()));

        CreateMap<SymbolInfo, SymbolSearchResult>()
            .ForMember(d => d.Score,
                o => o.MapFrom((_, _, _, context) => context.Items.TryGetValue("Score", out var val) ? val : 0.0))
            .ForMember(d => d.ContainingType,
                o => o.MapFrom((_, _, _, context) =>
                    context.Items.TryGetValue("ContainingType", out var val) ? val : null))
            .ForMember(d => d.ContainingNamespace,
                o => o.MapFrom((_, _, _, context) =>
                    context.Items.TryGetValue("ContainingNamespace", out var val) ? val : null));
    }

    // ==================== 辅助方法 ====================

    /// <summary>
    /// 通用转换逻辑：获取 SemanticModel -> 获取 Symbol -> 映射为 Model
    /// </summary>
    private static TDest? ConvertViaSymbol<TSyntax, TDest>(
        TSyntax syntax,
        ResolutionContext context,
        Func<SemanticModel, TSyntax, ISymbol?> symbolSelector)
        where TSyntax : SyntaxNode
    {
        var semanticModel = GetSemanticModel(context);
        if (semanticModel == null) return default;

        // 使用传入的委托获取 Symbol (兼容 FieldDeclaration 等特殊情况)
        var symbol = symbolSelector(semanticModel, syntax);

        if (symbol == null) return default;

        // 委托 AutoMapper 执行 ISymbol -> TDest 的映射
        return context.Mapper.Map<TDest>(symbol);
    }

    /// <summary>
    /// 统一处理只能从 Syntax 此时获取的补充属性（如 Summary）
    /// </summary>
    private static void ApplySyntaxInfo(MemberDeclarationSyntax src, SymbolInfo? dest)
    {
        if (dest == null) return;
        dest.Summary = src.GetXmlComment();
    }


    private static LocationView MapLocation(Location loc)
    {
        if (!loc.IsInSource) return null!;
        var span = loc.GetLineSpan();
        return new LocationView
        {
            FilePath = span.Path,
            StartLine = span.StartLinePosition.Line + 1,
            EndLine = span.EndLinePosition.Line + 1
        };
    }


    private static List<ParameterInfo> MapParameters(ImmutableArray<IParameterSymbol> parameters)
    {
        return parameters.Select(p => new ParameterInfo
        {
            Name = p.Name,
            Type = FormatTypeName(p.Type),
            HasDefaultValue = p.HasExplicitDefaultValue,
            DefaultValue = p.HasExplicitDefaultValue
                ? p.ExplicitDefaultValue?.ToString()
                : null
        }).ToList();
    }

    private static SemanticModel? GetSemanticModel(ResolutionContext context)
    {
        return context.Items.TryGetValue("SemanticModel", out var model) && model is SemanticModel semanticModel
            ? semanticModel
            : null;
    }


    private static string FormatTypeName(ITypeSymbol? type)
    {
        if (type == null) return "unknown";

        var displayString = type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
        displayString = displayString
            .Replace("System.Collections.Generic.", "")
            .Replace("System.Threading.Tasks.", "")
            .Replace("System.", "");

        return displayString;
    }
}