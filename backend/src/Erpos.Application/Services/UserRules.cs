using Erpos.Application.Authorization;
using Erpos.Application.Common;
using Erpos.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Erpos.Application.Services;

/// <summary>Rules shared by everything that creates logins or hands out roles (Users and HR Employees).</summary>
public class UserRules(IAppDbContext db, IAccessService access, ICurrentUser currentUser)
{
    public const string BaselineRoleName = "Employee";

    /// <summary>
    /// Super Admin manages anyone in the organization; Admin manages Admins, Managers and Employees;
    /// Manager manages Employees only.
    /// </summary>
    public void EnsureCanManageType(UserType target)
    {
        if (!CanManageType(target)) throw new ForbiddenException($"You cannot manage users of type {target}.");
    }

    public bool CanManageType(UserType target) => currentUser.UserType switch
    {
        UserType.SuperAdmin => target is UserType.SuperAdmin or UserType.Admin or UserType.Manager or UserType.Employee,
        UserType.Admin => target is UserType.Admin or UserType.Manager or UserType.Employee,
        UserType.Manager => target is UserType.Employee,
        _ => false
    };

    /// <summary>Only the Super Admin may change their own roles or overrides; everyone else asks another administrator.</summary>
    public void EnsureNotSelf(Guid userId)
    {
        if (userId == currentUser.UserId && !currentUser.IsSuperAdmin)
            throw new ForbiddenException("You can't change your own access. Ask another administrator.");
    }

    /// <summary>
    /// Prevents privilege escalation: the assigner must hold every permission in the role at the entity and, when the
    /// assignment covers sub-entities, at every one of them too.
    /// </summary>
    public async Task EnsureCanAssignRoleAsync(Guid roleId, Guid entityId, CancellationToken ct, bool includeDescendants = true)
    {
        await access.EnsureAsync(Permissions.UsersAssign, entityId, ct);
        var role = await db.Roles.Include(r => r.Permissions).FirstOrDefaultAsync(r => r.Id == roleId, ct)
                   ?? throw new NotFoundException("Role");
        if (!await db.Entities.AnyAsync(e => e.Id == entityId, ct)) throw new NotFoundException("Entity");
        if (currentUser.IsSuperAdmin) return;

        var missing = await MissingAsync(role.Permissions.Select(p => p.PermissionCode).ToList(), entityId, includeDescendants, ct);
        if (missing.Count > 0)
            throw new ForbiddenException($"You can't assign '{role.Name}' {(includeDescendants ? "here and below" : "here")} because you don't hold all of its " +
                                         $"permissions ({missing.Count} missing{(missing.Any(m => m.EntityId != entityId) ? ", some at sub-entities" : "")}).");
    }

    /// <summary>Grants or denials can only cover entities where the assigner holds the permission themselves.</summary>
    public async Task EnsureCanSetOverrideAsync(string permissionCode, Guid entityId, bool includeDescendants, CancellationToken ct)
    {
        await access.EnsureAsync(Permissions.UsersAssign, entityId, ct);
        if (currentUser.IsSuperAdmin) return;
        if ((await MissingAsync([permissionCode], entityId, includeDescendants, ct)).Count > 0)
            throw new ForbiddenException($"You don't hold '{permissionCode}' {(includeDescendants ? "at this entity and all below it" : "at this entity")}.");
    }

    /// <summary>Permissions (of modules switched on there) the current user lacks at the entity, or at it and its sub-entities.</summary>
    private async Task<List<(string Code, Guid EntityId)>> MissingAsync(List<string> codes, Guid entityId, bool includeDescendants, CancellationToken ct)
    {
        var path = await db.Entities.Where(e => e.Id == entityId).Select(e => e.Path).FirstOrDefaultAsync(ct) ?? throw new NotFoundException("Entity");
        var scope = includeDescendants
            ? await db.Entities.Where(e => e.Path.StartsWith(path)).Select(e => e.Id).ToListAsync(ct)
            : [entityId];
        // Permissions of modules that are off at an entity are inert for everyone, so they don't count.
        var enabled = (await db.EntityModules.Where(m => scope.Contains(m.EntityId) && m.IsEnabled).Select(m => new { m.EntityId, m.ModuleCode }).ToListAsync(ct))
            .GroupBy(m => m.EntityId).ToDictionary(g => g.Key, g => g.Select(m => m.ModuleCode).ToHashSet());
        var mine = await access.CurrentAsync(ct);
        return (from e in scope
                from c in codes
                let module = Permissions.ModuleOf(c)
                where Modules.IsAlwaysOn(module) || (enabled.TryGetValue(e, out var mods) && mods.Contains(module))
                where !mine.Has(c, e)
                select (c, e)).ToList();
    }

    /// <summary>Ends every session of a user (password change or reset, deactivation, deletion, stolen-token response). Caller saves.</summary>
    public static async Task RevokeSessionsAsync(IAppDbContext db, Guid userId, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        foreach (var t in await db.RefreshTokens.Where(t => t.UserId == userId && t.RevokedAt == null).ToListAsync(ct)) t.RevokedAt = now;
    }

    /// <summary>The default self-service role every new employee login receives.</summary>
    public async Task<Guid?> BaselineRoleIdAsync(CancellationToken ct) =>
        await db.Roles.Where(r => r.IsSystem && r.Name == BaselineRoleName).Select(r => (Guid?)r.Id).FirstOrDefaultAsync(ct);

    public async Task EnsureSeatAvailableAsync(CancellationToken ct)
    {
        var tenantId = currentUser.TenantId!.Value;
        var max = await db.Tenants.Where(t => t.Id == tenantId).Select(t => t.MaxUsers).FirstAsync(ct);
        if (await db.Users.CountAsync(ct) >= max)
            throw new ValidationException($"Your plan allows {max} users. Contact the platform administrator to raise it.");
    }

    public async Task EnsureEmailFreeAsync(string email, CancellationToken ct)
    {
        if (await db.Users.IgnoreQueryFilters().AnyAsync(u => u.Email == email && !u.IsDeleted, ct))
            throw new ValidationException($"Email '{email}' is already registered.");
    }
}
