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

    /// <summary>Prevents privilege escalation: the assigner must hold every permission in the role at that entity.</summary>
    public async Task EnsureCanAssignRoleAsync(Guid roleId, Guid entityId, CancellationToken ct)
    {
        await access.EnsureAsync(Permissions.UsersAssign, entityId, ct);
        var role = await db.Roles.Include(r => r.Permissions).FirstOrDefaultAsync(r => r.Id == roleId, ct)
                   ?? throw new NotFoundException("Role");
        if (!await db.Entities.AnyAsync(e => e.Id == entityId, ct)) throw new NotFoundException("Entity");
        if (currentUser.IsSuperAdmin) return;

        // Permissions of modules that are off at this entity are inert for everyone, so they don't count.
        var enabled = await db.EntityModules.Where(m => m.EntityId == entityId && m.IsEnabled)
            .Select(m => m.ModuleCode).ToListAsync(ct);
        var mine = await access.CurrentAsync(ct);
        var missing = role.Permissions.Select(p => p.PermissionCode)
            .Where(c => Modules.IsAlwaysOn(Permissions.ModuleOf(c)) || enabled.Contains(Permissions.ModuleOf(c)))
            .Where(c => !mine.Has(c, entityId)).ToList();
        if (missing.Count > 0)
            throw new ForbiddenException(
                $"You can't assign '{role.Name}' here because you don't hold all of its permissions ({missing.Count} missing).");
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
