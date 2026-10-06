using Erpos.Application.Authorization;
using Erpos.Application.Common;
using Erpos.Application.Dtos;
using Erpos.Domain.Entities;
using Erpos.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Erpos.Application.Services;

public class UserService(IAppDbContext db, IAccessService access, ICurrentUser currentUser, IPasswordHasher hasher,
    UserRules rules)
{
    public async Task<PagedResult<UserDto>> ListAsync(Guid? entityId, bool includeSubEntities, string? search,
        int page, int pageSize, CancellationToken ct)
    {
        var visible = await access.EntitiesWithAsync(Permissions.UsersView, ct);
        var q = db.Users.Include(u => u.PrimaryEntity)
            .Where(u => u.PrimaryEntityId != null && visible.Contains(u.PrimaryEntityId.Value));

        if (entityId is { } eid)
        {
            if (includeSubEntities)
            {
                var path = await db.Entities.Where(e => e.Id == eid).Select(e => e.Path).FirstOrDefaultAsync(ct)
                           ?? throw new NotFoundException("Entity");
                q = q.Where(u => u.PrimaryEntity!.Path.StartsWith(path));
            }
            else q = q.Where(u => u.PrimaryEntityId == eid);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            q = q.Where(u => u.FullName.ToLower().Contains(s) || u.Email.Contains(s));
        }

        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 200);
        var total = await q.CountAsync(ct);
        var items = await q.OrderBy(u => u.FullName).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new PagedResult<UserDto>(items.Select(u => u.ToDto()).ToList(), total, page, pageSize);
    }

    public async Task<UserDto> GetAsync(Guid id, CancellationToken ct)
    {
        var user = await LoadVisibleAsync(id, Permissions.UsersView, ct);
        return user.ToDto();
    }

    public async Task<UserDto> CreateAsync(CreateUserRequest req, CancellationToken ct)
    {
        await access.EnsureAsync(Permissions.UsersCreate, req.PrimaryEntityId, ct);
        rules.EnsureCanManageType(req.UserType);
        Guard.Password(req.Password);
        var email = Guard.Email(req.Email);

        await rules.EnsureEmailFreeAsync(email, ct);
        if (!await db.Entities.AnyAsync(e => e.Id == req.PrimaryEntityId, ct)) throw new NotFoundException("Entity");

        var tenantId = currentUser.TenantId!.Value;
        await rules.EnsureSeatAvailableAsync(ct);

        var user = new User
        {
            TenantId = tenantId, Email = email, FullName = Guard.Required(req.FullName, "Full name"), Phone = req.Phone,
            UserType = req.UserType, PasswordHash = hasher.Hash(req.Password), PrimaryEntityId = req.PrimaryEntityId
        };
        db.Users.Add(user);

        if (req.RoleId is { } roleId)
        {
            await rules.EnsureCanAssignRoleAsync(roleId, req.PrimaryEntityId, ct);
            db.UserRoleAssignments.Add(new UserRoleAssignment
            {
                TenantId = tenantId, UserId = user.Id, RoleId = roleId, EntityId = req.PrimaryEntityId
            });
        }

        await db.SaveChangesAsync(ct);
        return await GetAsync(user.Id, ct);
    }

    public async Task<UserDto> UpdateAsync(Guid id, UpdateUserRequest req, CancellationToken ct)
    {
        var user = await LoadVisibleAsync(id, Permissions.UsersEdit, ct);
        rules.EnsureCanManageType(user.UserType);
        if (user.Id == currentUser.UserId && (req.UserType != user.UserType || !req.IsActive))
            throw new ValidationException("You cannot change your own user type or deactivate yourself.");
        rules.EnsureCanManageType(req.UserType);

        if (req.PrimaryEntityId != user.PrimaryEntityId)
            await access.EnsureAsync(Permissions.UsersCreate, req.PrimaryEntityId, ct);

        user.FullName = Guard.Required(req.FullName, "Full name");
        user.Phone = req.Phone;
        user.UserType = req.UserType;
        user.PrimaryEntityId = req.PrimaryEntityId;
        if (user.IsActive && !req.IsActive) await UserRules.RevokeSessionsAsync(db, user.Id, ct);
        user.IsActive = req.IsActive;
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var user = await LoadVisibleAsync(id, Permissions.UsersDelete, ct);
        rules.EnsureCanManageType(user.UserType);
        if (user.Id == currentUser.UserId) throw new ValidationException("You cannot delete your own account.");
        user.IsDeleted = true;
        user.IsActive = false;
        await UserRules.RevokeSessionsAsync(db, user.Id, ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task ResetPasswordAsync(Guid id, ResetPasswordRequest req, CancellationToken ct)
    {
        var user = await LoadVisibleAsync(id, Permissions.UsersEdit, ct);
        rules.EnsureCanManageType(user.UserType);
        Guard.Password(req.NewPassword);
        user.PasswordHash = hasher.Hash(req.NewPassword);
        user.FailedLoginCount = 0;
        user.LockedUntil = null; // an admin reset also unlocks the account
        await UserRules.RevokeSessionsAsync(db, user.Id, ct);
        await db.SaveChangesAsync(ct);
    }

    // ---------- Access: role assignments and overrides ----------

    public async Task<UserAccessDto> GetAccessAsync(Guid id, CancellationToken ct)
    {
        await LoadVisibleAsync(id, Permissions.UsersView, ct);
        var assignments = await db.UserRoleAssignments.Where(a => a.UserId == id)
            .Select(a => new AssignmentDto(a.Id, a.RoleId, a.Role!.Name, a.EntityId, a.Entity!.Name, a.IncludeDescendants))
            .ToListAsync(ct);
        var overrides = await db.UserPermissionOverrides.Where(o => o.UserId == id)
            .Select(o => new OverrideDto(o.Id, o.PermissionCode, o.EntityId, o.Entity!.Name, o.IsGranted, o.IncludeDescendants))
            .ToListAsync(ct);
        var effective = await access.ForUserAsync(id, ct);
        return new UserAccessDto(assignments, overrides,
            effective.ByEntity.ToDictionary(kv => kv.Key, kv => (IReadOnlyList<string>)kv.Value.OrderBy(c => c).ToList()));
    }

    public async Task<UserAccessDto> AddAssignmentAsync(Guid userId, CreateAssignmentRequest req, CancellationToken ct)
    {
        var user = await LoadVisibleAsync(userId, Permissions.UsersAssign, ct);
        rules.EnsureCanManageType(user.UserType);
        rules.EnsureNotSelf(userId);
        await rules.EnsureCanAssignRoleAsync(req.RoleId, req.EntityId, ct, req.IncludeDescendants);

        if (await db.UserRoleAssignments.AnyAsync(a => a.UserId == userId && a.RoleId == req.RoleId && a.EntityId == req.EntityId, ct))
            throw new ValidationException("The user already has this role at this entity.");

        db.UserRoleAssignments.Add(new UserRoleAssignment
        {
            TenantId = user.TenantId!.Value, UserId = userId, RoleId = req.RoleId, EntityId = req.EntityId,
            IncludeDescendants = req.IncludeDescendants
        });
        await db.SaveChangesAsync(ct);
        return await GetAccessAsync(userId, ct);
    }

    public async Task<UserAccessDto> RemoveAssignmentAsync(Guid userId, Guid assignmentId, CancellationToken ct)
    {
        var user = await LoadVisibleAsync(userId, Permissions.UsersAssign, ct);
        rules.EnsureCanManageType(user.UserType);
        rules.EnsureNotSelf(userId);
        var a = await db.UserRoleAssignments.FirstOrDefaultAsync(x => x.Id == assignmentId && x.UserId == userId, ct)
                ?? throw new NotFoundException("Assignment");
        await access.EnsureAsync(Permissions.UsersAssign, a.EntityId, ct);
        db.UserRoleAssignments.Remove(a);
        await db.SaveChangesAsync(ct);
        return await GetAccessAsync(userId, ct);
    }

    public async Task<UserAccessDto> AddOverrideAsync(Guid userId, CreateOverrideRequest req, CancellationToken ct)
    {
        var user = await LoadVisibleAsync(userId, Permissions.UsersAssign, ct);
        rules.EnsureCanManageType(user.UserType);
        rules.EnsureNotSelf(userId);
        if (!Permissions.Exists(req.PermissionCode)) throw new ValidationException("Unknown permission.");
        // You can only hand out (or take away) what you hold yourself, everywhere the override reaches.
        await rules.EnsureCanSetOverrideAsync(req.PermissionCode, req.EntityId, req.IncludeDescendants, ct);

        var existing = await db.UserPermissionOverrides.FirstOrDefaultAsync(o =>
            o.UserId == userId && o.EntityId == req.EntityId && o.PermissionCode == req.PermissionCode, ct);
        if (existing != null)
        {
            existing.IsGranted = req.IsGranted;
            existing.IncludeDescendants = req.IncludeDescendants;
        }
        else
        {
            db.UserPermissionOverrides.Add(new UserPermissionOverride
            {
                TenantId = user.TenantId!.Value, UserId = userId, EntityId = req.EntityId,
                PermissionCode = req.PermissionCode, IsGranted = req.IsGranted, IncludeDescendants = req.IncludeDescendants
            });
        }
        await db.SaveChangesAsync(ct);
        return await GetAccessAsync(userId, ct);
    }

    public async Task<UserAccessDto> RemoveOverrideAsync(Guid userId, Guid overrideId, CancellationToken ct)
    {
        var user = await LoadVisibleAsync(userId, Permissions.UsersAssign, ct);
        rules.EnsureCanManageType(user.UserType);
        rules.EnsureNotSelf(userId); // removing your own "deny" would be an escalation
        var o = await db.UserPermissionOverrides.FirstOrDefaultAsync(x => x.Id == overrideId && x.UserId == userId, ct)
                ?? throw new NotFoundException("Override");
        // Removing a denial widens access, so it needs the same rights as granting.
        if (o.IsGranted) await access.EnsureAsync(Permissions.UsersAssign, o.EntityId, ct);
        else await rules.EnsureCanSetOverrideAsync(o.PermissionCode, o.EntityId, o.IncludeDescendants, ct);
        db.UserPermissionOverrides.Remove(o);
        await db.SaveChangesAsync(ct);
        return await GetAccessAsync(userId, ct);
    }

    // ---------- helpers ----------

    private async Task<User> LoadVisibleAsync(Guid id, string permission, CancellationToken ct)
    {
        var user = await db.Users.Include(u => u.PrimaryEntity).FirstOrDefaultAsync(u => u.Id == id, ct)
                   ?? throw new NotFoundException("User");
        if (user.PrimaryEntityId is not { } entityId) throw new ForbiddenException();
        await access.EnsureAsync(permission, entityId, ct);
        return user;
    }
}
