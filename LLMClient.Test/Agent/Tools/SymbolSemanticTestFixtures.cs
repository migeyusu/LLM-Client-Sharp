using LLMClient.ContextEngineering.Analysis;
using Microsoft.CodeAnalysis.Text;

namespace LLMClient.Test.Agent.Tools;

/// <summary>
/// 扩展 TestFixtures：构建含完整符号树的测试数据。
/// 继承 TestFixtures 的路径常量，保持一致性。
/// </summary>
internal static class SymbolSemanticTestFixtures
{
    // ── 符号 ID 常量（测试断言复用）────────────────────────────────────

    public const string UserServiceTypeId = "T:MyApp.Core.Services.UserService";
    public const string IUserServiceTypeId = "T:MyApp.Core.Services.IUserService";
    public const string UserTypeId = "T:MyApp.Core.Models.User";
    public const string OrderServiceTypeId = "T:MyApp.Core.Services.OrderService";

    public const string SaveAsyncMemberId = "M:MyApp.Core.Services.UserService.SaveAsync(MyApp.Core.Models.User)";
    public const string GetByIdMemberId = "M:MyApp.Core.Services.UserService.GetByIdAsync(System.Int32)";
    public const string DeleteMemberId = "M:MyApp.Core.Services.UserService.Delete(System.Int32)";
    public const string UserIdPropId = "P:MyApp.Core.Models.User.Id";

    // ── 主入口 ────────────────────────────────────────────────────────

    /// <summary>构建含三个命名空间、两个项目、完整类型树的 SolutionInfo。</summary>
    public static SolutionInfo BuildRichSolution()
    {
        var core = BuildRichCoreProject();
        var api = BuildRichApiProject();
        return TestFixtures.BuildSolution(core, api);
    }

    // ── Core Project ──────────────────────────────────────────────────

    public static ProjectInfo BuildRichCoreProject()
    {
        var p = TestFixtures.BuildCoreProject();   // 继承基础文件索引
        p.Namespaces.Clear();                       // 替换为更丰富的类型树

        p.Namespaces.Add(BuildServicesNamespace());
        p.Namespaces.Add(BuildModelsNamespace());
        p.Namespaces.Add(BuildInterfacesNamespace());

        return p;
    }

    // ── Namespaces ────────────────────────────────────────────────────

    private static NamespaceInfo BuildServicesNamespace()
    {
        var ns = new NamespaceInfo
        {
            Name = "MyApp.Core.Services",
            FilePath = TestFixtures.CoreProjectPath
        };
        ns.Types.Add(BuildUserServiceType());
        ns.Types.Add(BuildOrderServiceType());
        return ns;
    }

    private static NamespaceInfo BuildModelsNamespace()
    {
        var ns = new NamespaceInfo
        {
            Name = "MyApp.Core.Models",
            FilePath = TestFixtures.CoreProjectPath
        };
        ns.Types.Add(BuildUserType());
        return ns;
    }

    private static NamespaceInfo BuildInterfacesNamespace()
    {
        var ns = new NamespaceInfo
        {
            Name = "MyApp.Core.Services",   // 同一命名空间，跨文件
            FilePath = @"C:\Projects\MyApp\MyApp.Core\Interfaces\IUserService.cs"
        };
        ns.Types.Add(BuildIUserServiceType());
        return ns;
    }

    // ── Types ─────────────────────────────────────────────────────────

    public static TypeInfo BuildUserServiceType() => new()
    {
        UniqueId = UserServiceTypeId,
        Name = "UserService",
        Kind = "Class",
        Signature = "public class UserService",
        Accessibility = "Public",
        Summary = "Handles user business logic.",
        FilePath = @"C:\Projects\MyApp\MyApp.Core\Services\UserService.cs",
        RelativePath = @"Services\UserService.cs",
        LineNumber = 5,
        IsPartial = false,
        IsAbstract = false,
        IsSealed = false,
        ImplementedInterfaces = ["IUserService"],
        Locations = [Loc(@"C:\Projects\MyApp\MyApp.Core\Services\UserService.cs", 5, 80)],
        Attributes = ["Injectable"],
        Members =
        {
            BuildSaveAsyncMember(),
            BuildGetByIdMember(),
            BuildDeleteMember(),
            BuildPrivateHelperMember()
        }
    };

    public static TypeInfo BuildOrderServiceType() => new()
    {
        UniqueId = OrderServiceTypeId,
        Name = "OrderService",
        Kind = "Class",
        Signature = "public class OrderService",
        Accessibility = "Public",
        Summary = "Handles order processing.",
        FilePath = @"C:\Projects\MyApp\MyApp.Core\Services\OrderService.cs",
        RelativePath = @"Services\OrderService.cs",
        LineNumber = 1,
        IsPartial = false,
        IsAbstract = false,
        IsSealed = false,
        Locations = [Loc(@"C:\Projects\MyApp\MyApp.Core\Services\OrderService.cs", 1, 50)],
        Members =
        {
            BuildPlaceOrderMember()
        }
    };

    public static TypeInfo BuildUserType() => new()
    {
        UniqueId = UserTypeId,
        Name = "User",
        Kind = "Class",
        Signature = "public class User",
        Accessibility = "Public",
        Summary = "Domain model for a user.",
        FilePath = @"C:\Projects\MyApp\MyApp.Core\Models\User.cs",
        RelativePath = @"Models\User.cs",
        LineNumber = 1,
        Locations = [Loc(@"C:\Projects\MyApp\MyApp.Core\Models\User.cs", 1, 25)],
        Members =
        {
            BuildUserIdProp(),
            BuildUserNameProp()
        }
    };

    public static TypeInfo BuildIUserServiceType() => new()
    {
        UniqueId = IUserServiceTypeId,
        Name = "IUserService",
        Kind = "Interface",
        Signature = "public interface IUserService",
        Accessibility = "Public",
        Summary = "Contract for user operations.",
        FilePath = @"C:\Projects\MyApp\MyApp.Core\Interfaces\IUserService.cs",
        RelativePath = @"Interfaces\IUserService.cs",
        LineNumber = 1,
        Locations = [Loc(@"C:\Projects\MyApp\MyApp.Core\Interfaces\IUserService.cs", 1, 15)],
        Members =
        {
            BuildSaveAsyncInterfaceMember()
        }
    };

    // ── Members ────────────────────────────────────────────────────────

    public static MemberInfo BuildSaveAsyncMember() => new()
    {
        UniqueId = SaveAsyncMemberId,
        Name = "SaveAsync",
        Kind = "Method",
        Signature = "public async Task<bool> SaveAsync(User user)",
        Accessibility = "Public",
        ReturnType = "Task<bool>",
        IsAsync = true,
        IsStatic = false,
        IsVirtual = false,
        IsOverride = false,
        Comment = "Persists a user entity.",
        Parameters =
        [
            new ParameterInfo { Name = "user", Type = "User", DefaultValue = null }
        ],
        Locations = [Loc(@"C:\Projects\MyApp\MyApp.Core\Services\UserService.cs", 10, 25)],
        Attributes = []
    };

    public static MemberInfo BuildGetByIdMember() => new()
    {
        UniqueId = GetByIdMemberId,
        Name = "GetByIdAsync",
        Kind = "Method",
        Signature = "public async Task<User?> GetByIdAsync(int id)",
        Accessibility = "Public",
        ReturnType = "Task<User?>",
        IsAsync = true,
        IsStatic = false,
        IsVirtual = false,
        IsOverride = false,
        Comment = "Retrieves a user by primary key.",
        Parameters =
        [
            new ParameterInfo { Name = "id", Type = "int", DefaultValue = null }
        ],
        Locations = [Loc(@"C:\Projects\MyApp\MyApp.Core\Services\UserService.cs", 28, 40)],
        Attributes = []
    };

    public static MemberInfo BuildDeleteMember() => new()
    {
        UniqueId = DeleteMemberId,
        Name = "Delete",
        Kind = "Method",
        Signature = "public bool Delete(int id)",
        Accessibility = "Public",
        ReturnType = "bool",
        IsAsync = false,
        IsStatic = false,
        IsVirtual = false,
        IsOverride = false,
        Locations = [Loc(@"C:\Projects\MyApp\MyApp.Core\Services\UserService.cs", 43, 55)],
        Attributes = []
    };

    public static MemberInfo BuildPrivateHelperMember() => new()
    {
        UniqueId = "M:MyApp.Core.Services.UserService.ValidateInternal(MyApp.Core.Models.User)",
        Name = "ValidateInternal",
        Kind = "Method",
        Signature = "private bool ValidateInternal(User user)",
        Accessibility = "Private",
        ReturnType = "bool",
        IsAsync = false,
        IsStatic = false,
        IsVirtual = false,
        IsOverride = false,
        Locations = [Loc(@"C:\Projects\MyApp\MyApp.Core\Services\UserService.cs", 58, 70)],
        Attributes = []
    };

    private static MemberInfo BuildSaveAsyncInterfaceMember() => new()
    {
        UniqueId = "M:MyApp.Core.Services.IUserService.SaveAsync(MyApp.Core.Models.User)",
        Name = "SaveAsync",
        Kind = "Method",
        Signature = "Task<bool> SaveAsync(User user)",
        Accessibility = "Public",
        ReturnType = "Task<bool>",
        IsAsync = false,
        IsStatic = false,
        IsVirtual = false,
        IsOverride = false,
        Locations = [Loc(@"C:\Projects\MyApp\MyApp.Core\Interfaces\IUserService.cs", 3, 4)],
        Attributes = []
    };

    private static MemberInfo BuildPlaceOrderMember() => new()
    {
        UniqueId = "M:MyApp.Core.Services.OrderService.PlaceOrderAsync(System.Int32)",
        Name = "PlaceOrderAsync",
        Kind = "Method",
        Signature = "public async Task PlaceOrderAsync(int userId)",
        Accessibility = "Public",
        ReturnType = "Task",
        IsAsync = true,
        IsStatic = false,
        IsVirtual = false,
        IsOverride = false,
        Locations = [Loc(@"C:\Projects\MyApp\MyApp.Core\Services\OrderService.cs", 5, 30)],
        Attributes = []
    };

    private static MemberInfo BuildUserIdProp() => new()
    {
        UniqueId = UserIdPropId,
        Name = "Id",
        Kind = "Property",
        Signature = "public int Id { get; set; }",
        Accessibility = "Public",
        ReturnType = "int",
        IsStatic = false,
        IsVirtual = false,
        IsOverride = false,
        Locations = [Loc(@"C:\Projects\MyApp\MyApp.Core\Models\User.cs", 5, 5)],
        Attributes = []
    };

    private static MemberInfo BuildUserNameProp() => new()
    {
        UniqueId = "P:MyApp.Core.Models.User.Name",
        Name = "Name",
        Kind = "Property",
        Signature = "public string Name { get; set; }",
        Accessibility = "Public",
        ReturnType = "string",
        IsStatic = false,
        IsVirtual = false,
        IsOverride = false,
        Locations = [Loc(@"C:\Projects\MyApp\MyApp.Core\Models\User.cs", 6, 6)],
        Attributes = []
    };

    // ── API Project ────────────────────────────────────────────────────

    private static ProjectInfo BuildRichApiProject()
    {
        var p = TestFixtures.BuildApiProject();
        p.Namespaces.Add(new NamespaceInfo
        {
            Name = "MyApp.Api.Controllers",
            FilePath = TestFixtures.ApiProjectPath,
            Types =
            {
                new TypeInfo
                {
                    UniqueId = "T:MyApp.Api.Controllers.UserController",
                    Name = "UserController",
                    Kind = "Class",
                    Signature = "public class UserController",
                    Accessibility = "Public",
                    FilePath = @"C:\Projects\MyApp\MyApp.Api\Controllers\UserController.cs",
                    RelativePath = @"Controllers\UserController.cs",
                    LineNumber = 1,
                    Locations = [Loc(@"C:\Projects\MyApp\MyApp.Api\Controllers\UserController.cs", 1, 60)],
                    Members =
                    {
                        new MemberInfo
                        {
                            UniqueId = "M:MyApp.Api.Controllers.UserController.GetUser(System.Int32)",
                            Name = "GetUser",
                            Kind = "Method",
                            Signature = "public async Task<IActionResult> GetUser(int id)",
                            Accessibility = "Public",
                            ReturnType = "Task<IActionResult>",
                            IsAsync = true,
                            IsStatic = false,
                            IsVirtual = false,
                            IsOverride = false,
                            Locations =
                                [Loc(@"C:\Projects\MyApp\MyApp.Api\Controllers\UserController.cs", 10, 25)],
                            Attributes = ["HttpGet"]
                        }
                    }
                }
            }
        });
        return p;
    }

    // ── 工具方法 ──────────────────────────────────────────────────────

    private static CodeLocation Loc(string file, int start, int end) => new()
    {
        FilePath = file,
        Location = new LinePositionSpan(
            new LinePosition(start, 1),
            new LinePosition(end, 1))
    };
}