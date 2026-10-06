using Erpos.Application.Authorization;
using Erpos.Domain.Enums;

namespace Erpos.Application.Dtos;

public record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);

// ---- Auth ----
public record LoginRequest(string Email, string Password);
public record RefreshRequest(string RefreshToken);
public record ChangePasswordRequest(string CurrentPassword, string NewPassword);
public record AuthResponse(string AccessToken, DateTime ExpiresAt, string RefreshToken, UserDto User);
public record EntityAccessDto(Guid Id, Guid? ParentId, string Name, string Code, IndustryType Industry, int Depth,
    bool IsActive, IReadOnlyList<string> Modules, IReadOnlyList<string> Permissions);
public record MeResponse(UserDto User, TenantDto? Tenant, IReadOnlyList<EntityAccessDto> Entities);

// ---- Platform / tenants ----
public record TenantDto(Guid Id, string Name, string Code, TenantStatus Status, string? ContactEmail, string? ContactPhone,
    string? Country, int MaxUsers, DateTime CreatedAt, int UserCount, int EntityCount);
public record CreateTenantRequest(string Name, string Code, IndustryType Industry, string? ContactEmail, string? ContactPhone,
    string? Country, int MaxUsers, string Currency, string TimeZone, List<string>? Modules,
    string SuperAdminName, string SuperAdminEmail, string SuperAdminPassword);
public record UpdateTenantRequest(string Name, TenantStatus Status, string? ContactEmail, string? ContactPhone,
    string? Country, int MaxUsers);

// ---- Entities ----
public record EntityDto(Guid Id, Guid? ParentId, string Name, string Code, IndustryType Industry, int Depth, bool IsActive,
    string? Email, string? Phone, string? Address, string? City, string? Country, string Currency, string TimeZone,
    string? TaxNumber, int ChildCount, int UserCount, IReadOnlyList<string> Modules);
public record SaveEntityRequest(string Name, string Code, IndustryType Industry, bool IsActive, string? Email, string? Phone,
    string? Address, string? City, string? Country, string Currency, string TimeZone, string? TaxNumber);
public record CreateEntityRequest(Guid ParentId, SaveEntityRequest Data, List<string>? Modules);
public record MoveEntityRequest(Guid NewParentId);
public record SetModulesRequest(List<string> Modules);
public record ModuleStateDto(string Code, string Name, string Description, bool AlwaysOn, bool Enabled,
    bool AvailableFromParent, bool Suggested);

// ---- Users ----
public record UserDto(Guid Id, string Email, string FullName, string? Phone, UserType UserType, bool IsActive,
    Guid? PrimaryEntityId, string? PrimaryEntityName, DateTime? LastLoginAt, DateTime CreatedAt);
public record CreateUserRequest(string Email, string FullName, string? Phone, string Password, UserType UserType,
    Guid PrimaryEntityId, Guid? RoleId);
public record UpdateUserRequest(string FullName, string? Phone, UserType UserType, Guid PrimaryEntityId, bool IsActive);
public record ResetPasswordRequest(string NewPassword);
public record AssignmentDto(Guid Id, Guid RoleId, string RoleName, Guid EntityId, string EntityName, bool IncludeDescendants);
public record CreateAssignmentRequest(Guid RoleId, Guid EntityId, bool IncludeDescendants = true);
public record OverrideDto(Guid Id, string PermissionCode, Guid EntityId, string EntityName, bool IsGranted, bool IncludeDescendants);
public record CreateOverrideRequest(string PermissionCode, Guid EntityId, bool IsGranted, bool IncludeDescendants = true);
public record UserAccessDto(IReadOnlyList<AssignmentDto> Assignments, IReadOnlyList<OverrideDto> Overrides,
    IReadOnlyDictionary<Guid, IReadOnlyList<string>> Effective);

// ---- Roles ----
public record RoleDto(Guid Id, string Name, string? Description, bool IsSystem, IReadOnlyList<string> Permissions, int AssignmentCount);
public record SaveRoleRequest(string Name, string? Description, List<string> Permissions);

// ---- Catalog ----
public record PermissionGroupDto(string Module, string ModuleName, IReadOnlyList<PermissionDef> Permissions);

// ---- Audit / dashboard ----
public record AuditLogDto(long Id, Guid? UserId, string? UserName, string Action, string TableName, string? RecordId,
    string? Changes, DateTime Timestamp);
public record DashboardDto(int Entities, int Users, int ActiveUsers, int Roles, IReadOnlyDictionary<string, int> UsersByType,
    IReadOnlyDictionary<string, int> EntitiesByIndustry);
