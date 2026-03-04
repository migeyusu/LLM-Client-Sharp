using LLMClient.ContextEngineering.Analysis;
using Microsoft.CodeAnalysis.Text;

namespace LLMClient.Test.Agent.Tools;

/// <summary>
/// 为符号语义分析测试提供富数据 SolutionInfo。
/// 与 TestFixtures 互补：TestFixtures 侧重项目/文件结构，本类侧重命名空间/类型/成员。
/// </summary>
internal static class SymbolSemanticFixtures
{
    // ── DocumentId 常量 ──────────────────────────────────────────────────
    public const string UserServiceId      = "T:MyApp.Core.Services.UserService";
    public const string IUserServiceId     = "T:MyApp.Core.Services.IUserService";
    public const string OrderServiceId     = "T:MyApp.Core.Services.OrderService";
    public const string UserModelId        = "T:MyApp.Core.Models.User";
    public const string UserControllerId   = "T:MyApp.Api.Controllers.UserController";

    public const string SaveAsyncId        = "M:MyApp.Core.Services.UserService.SaveAsync(MyApp.Core.Models.User)";
    public const string GetByIdId          = "M:MyApp.Core.Services.UserService.GetById(System.Int32)";
    public const string NamePropertyId     = "P:MyApp.Core.Services.UserService.Name";
    public const string RepositoryFieldId  = "F:MyApp.Core.Services.UserService._repository";

    // ── Member 构建器 ────────────────────────────────────────────────────

    public static MemberInfo SaveAsync() => new()
    {
        UniqueId      = SaveAsyncId,
        Name          = "SaveAsync",
        Kind          = "Method",
        Signature     = "public async Task<bool> SaveAsync(User user)",
        Accessibility = "Public",
        IsAsync       = true,
        IsVirtual     = true,
        IsOverride    = false,
        IsStatic      = false,
        ReturnType    = "Task<bool>",
        Summary       = "Saves a user entity asynchronously.",
        Parameters    = [new() { Name = "user", Type = "User", HasDefaultValue = false, DefaultValue = null }],
        Attributes    = [],
        Locations     = [Loc(@"C:\Projects\MyApp\MyApp.Core\Services\UserService.cs", 20, 30)]
    };

    public static MemberInfo GetById() => new()
    {
        UniqueId      = GetByIdId,
        Name          = "GetById",
        Kind          = "Method",
        Signature     = "public User? GetById(int id)",
        Accessibility = "Public",
        IsAsync       = false,
        IsVirtual     = false,
        IsOverride    = false,
        IsStatic      = false,
        ReturnType    = "User?",
        Summary       = "Gets user by identifier.",
        Parameters    = [new() { Name = "id", Type = "int", HasDefaultValue = false, DefaultValue = null }],
        Attributes    = [],
        Locations     = [Loc(@"C:\Projects\MyApp\MyApp.Core\Services\UserService.cs", 35, 40)]
    };

    public static MemberInfo NameProperty() => new()
    {
        UniqueId      = NamePropertyId,
        Name          = "Name",
        Kind          = "Property",
        Signature     = "public string Name { get; set; }",
        Accessibility = "Public",
        IsAsync       = false,
        IsVirtual     = false,
        IsOverride    = false,
        IsStatic      = false,
        ReturnType    = "string",
        Attributes    = [],
        Locations     = [Loc(@"C:\Projects\MyApp\MyApp.Core\Services\UserService.cs", 10, 10)]
    };

    public static MemberInfo RepositoryField() => new()
    {
        UniqueId      = RepositoryFieldId,
        Name          = "_repository",
        Kind          = "Field",
        Signature     = "private readonly IUserRepository _repository",
        Accessibility = "Private",
        IsAsync       = false,
        IsVirtual     = false,
        IsOverride    = false,
        IsStatic      = false,
        ReturnType    = "IUserRepository",
        Attributes    = [],
        Locations     = [Loc(@"C:\Projects\MyApp\MyApp.Core\Services\UserService.cs", 5, 5)]
    };

    // ── Type 构建器 ──────────────────────────────────────────────────────

    public static TypeInfo UserService()
    {
        var t = new TypeInfo
        {
            UniqueId              = UserServiceId,
            Name                  = "UserService",
            Kind                  = "Class",
            Signature             = "public class UserService",
            Accessibility         = "Public",
            Summary               = "Handles user business logic.",
            IsPartial             = false,
            IsAbstract            = false,
            IsSealed              = false,
            ImplementedInterfaces = ["IUserService"],
            Attributes            = [],
            Locations             = [Loc(@"C:\Projects\MyApp\MyApp.Core\Services\UserService.cs", 1, 55)]
        };
        t.Members.Add(SaveAsync());
        t.Members.Add(GetById());
        t.Members.Add(NameProperty());
        t.Members.Add(RepositoryField());
        return t;
    }

    public static TypeInfo IUserService() => new()
    {
        UniqueId              = IUserServiceId,
        Name                  = "IUserService",
        Kind                  = "Interface",
        Signature             = "public interface IUserService",
        Accessibility         = "Public",
        Summary               = "Contract for user operations.",
        IsPartial             = false,
        IsAbstract            = false,
        IsSealed              = false,
        ImplementedInterfaces = [],
        Attributes            = [],
        Locations             = [Loc(@"C:\Projects\MyApp\MyApp.Core\Services\IUserService.cs", 1, 15)]
    };

    public static TypeInfo OrderService() => new()
    {
        UniqueId              = OrderServiceId,
        Name                  = "OrderService",
        Kind                  = "Class",
        Signature             = "public class OrderService",
        Accessibility         = "Public",
        Summary               = "Handles order business logic.",
        IsPartial             = false,
        IsAbstract            = false,
        IsSealed              = false,
        ImplementedInterfaces = [],
        Attributes            = [],
        Locations             = [Loc(@"C:\Projects\MyApp\MyApp.Core\Services\OrderService.cs", 1, 40)]
    };

    public static TypeInfo UserModel() => new()
    {
        UniqueId              = UserModelId,
        Name                  = "User",
        Kind                  = "Record",
        Signature             = "public record User",
        Accessibility         = "Public",
        Summary               = "Represents a user entity.",
        IsPartial             = false,
        IsAbstract            = false,
        IsSealed              = false,
        ImplementedInterfaces = [],
        Attributes            = [],
        Locations             = [Loc(@"C:\Projects\MyApp\MyApp.Core\Models\User.cs", 1, 10)]
    };

    public static TypeInfo UserController()
    {
        var t = new TypeInfo
        {
            UniqueId              = UserControllerId,
            Name                  = "UserController",
            Kind                  = "Class",
            Signature             = "public class UserController",
            Accessibility         = "Public",
            Summary               = "HTTP endpoints for user operations.",
            IsPartial             = false,
            IsAbstract            = false,
            IsSealed              = false,
            ImplementedInterfaces = [],
            Attributes            = ["ApiController", "Route"],
            Locations             = [Loc(@"C:\Projects\MyApp\MyApp.Api\Controllers\UserController.cs", 1, 80)]
        };
        return t;
    }

    // ── Solution 构建器 ──────────────────────────────────────────────────

    /// <summary>
    /// 含两个项目、四个命名空间、六个类型（含成员）的完整解决方案。
    /// 用于所有 SymbolSemanticService 测试。
    /// </summary>
    public static SolutionInfo BuildRichSolution()
    {
        var core = TestFixtures.BuildCoreProject();
        core.Namespaces.Clear(); // 替换 BuildCoreProject 内的简单占位数据

        var servicesNs = new NamespaceInfo
            { Name = "MyApp.Core.Services", FilePath = TestFixtures.CoreProjectPath };
        servicesNs.Types.Add(UserService());
        servicesNs.Types.Add(IUserService());
        servicesNs.Types.Add(OrderService());
        core.Namespaces.Add(servicesNs);

        var modelsNs = new NamespaceInfo
            { Name = "MyApp.Core.Models", FilePath = TestFixtures.CoreProjectPath };
        modelsNs.Types.Add(UserModel());
        core.Namespaces.Add(modelsNs);

        var api = TestFixtures.BuildApiProject();
        var controllersNs = new NamespaceInfo
            { Name = "MyApp.Api.Controllers", FilePath = TestFixtures.ApiProjectPath };
        controllersNs.Types.Add(UserController());
        api.Namespaces.Add(controllersNs);

        return TestFixtures.BuildSolution(core, api);
    }

    // ── 内部工具 ─────────────────────────────────────────────────────────

    public static CodeLocation Loc(string file, int startLine, int endLine) => new()
    {
        FilePath = file,
        Location = new LinePositionSpan(
            new LinePosition(startLine, 0),
            new LinePosition(endLine, 1))
    };
}