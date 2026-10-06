using Erpos.Domain.Common;
using Erpos.Domain.Enums;

namespace Erpos.Domain.Entities;

public class User : BaseEntity
{
    /// <summary>Null only for platform administrators.</summary>
    public Guid? TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    public string Email { get; set; } = "";
    public string FullName { get; set; } = "";
    public string? Phone { get; set; }
    public string PasswordHash { get; set; } = "";
    public UserType UserType { get; set; } = UserType.Employee;
    public bool IsActive { get; set; } = true;
    public DateTime? LastLoginAt { get; set; }
    /// <summary>Consecutive failed logins; reaching the limit locks the account for a while.</summary>
    public int FailedLoginCount { get; set; }
    public DateTime? LockedUntil { get; set; }

    /// <summary>The entity the user belongs to (their "home" branch/department).</summary>
    public Guid? PrimaryEntityId { get; set; }
    public BusinessEntity? PrimaryEntity { get; set; }

    public ICollection<UserRoleAssignment> RoleAssignments { get; set; } = new List<UserRoleAssignment>();
    public ICollection<UserPermissionOverride> PermissionOverrides { get; set; } = new List<UserPermissionOverride>();
}

/// <summary>A named bundle of permissions. Each organization owns its roles.</summary>
public class Role : BaseEntity, ITenantOwned
{
    public Guid TenantId { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    /// <summary>Default roles created with the organization; can be edited but not deleted.</summary>
    public bool IsSystem { get; set; }

    public ICollection<RolePermission> Permissions { get; set; } = new List<RolePermission>();
}

public class RolePermission
{
    public Guid RoleId { get; set; }
    public Role? Role { get; set; }
    public string PermissionCode { get; set; } = "";
}

/// <summary>Gives a user a role at an entity. With IncludeDescendants the role applies to every sub-entity too.</summary>
public class UserRoleAssignment : BaseEntity, IEntityScoped
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public User? User { get; set; }
    public Guid RoleId { get; set; }
    public Role? Role { get; set; }
    public Guid EntityId { get; set; }
    public BusinessEntity? Entity { get; set; }
    public bool IncludeDescendants { get; set; } = true;
}

/// <summary>Grants or denies one permission to one user at an entity, on top of their roles. Deny wins.</summary>
public class UserPermissionOverride : BaseEntity, IEntityScoped
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public User? User { get; set; }
    public Guid EntityId { get; set; }
    public BusinessEntity? Entity { get; set; }
    public string PermissionCode { get; set; } = "";
    public bool IsGranted { get; set; }
    public bool IncludeDescendants { get; set; } = true;
}

public class RefreshToken
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string TokenHash { get; set; } = "";
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? RevokedAt { get; set; }
}

public class AuditLog
{
    public long Id { get; set; }
    public Guid? TenantId { get; set; }
    public Guid? UserId { get; set; }
    public string Action { get; set; } = "";
    public string TableName { get; set; } = "";
    public string? RecordId { get; set; }
    public string? Changes { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
