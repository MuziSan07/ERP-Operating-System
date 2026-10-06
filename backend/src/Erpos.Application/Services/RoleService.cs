using Erpos.Application.Authorization;
using Erpos.Application.Common;
using Erpos.Application.Dtos;
using Erpos.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Erpos.Application.Services;

public class RoleService(IAppDbContext db, IAccessService access, ICurrentUser currentUser)
{
    public static List<PermissionGroupDto> Catalog() =>
        Modules.Catalog.Select(m => new PermissionGroupDto(m.Code, m.Name,
            Permissions.Catalog.Where(p => p.Module == m.Code).ToList())).ToList();

    public async Task<List<RoleDto>> ListAsync(CancellationToken ct)
    {
        // Roles are visible to anyone who can view roles or assign them to users.
        var mine = await access.CurrentAsync(ct);
        if (!mine.HasAnywhere(Permissions.RolesView) && !mine.HasAnywhere(Permissions.UsersAssign))
            throw new ForbiddenException();
        return await Project(db.Roles).ToListAsync(ct);
    }

    public async Task<RoleDto> GetAsync(Guid id, CancellationToken ct)
    {
        await access.EnsureAnywhereAsync(Permissions.RolesView, ct);
        return await Project(db.Roles.Where(r => r.Id == id)).FirstOrDefaultAsync(ct) ?? throw new NotFoundException("Role");
    }

    public async Task<RoleDto> CreateAsync(SaveRoleRequest req, CancellationToken ct)
    {
        await access.EnsureAtRootAsync(Permissions.RolesManage, ct); // roles apply organization-wide
        var role = new Role { TenantId = currentUser.TenantId!.Value };
        await ApplyAsync(role, req, [], ct);
        db.Roles.Add(role);
        await db.SaveChangesAsync(ct);
        return await GetAsync(role.Id, ct);
    }

    public async Task<RoleDto> UpdateAsync(Guid id, SaveRoleRequest req, CancellationToken ct)
    {
        await access.EnsureAtRootAsync(Permissions.RolesManage, ct); // roles apply organization-wide
        var role = await db.Roles.Include(r => r.Permissions).FirstOrDefaultAsync(r => r.Id == id, ct)
                   ?? throw new NotFoundException("Role");
        var assignedAt = await db.UserRoleAssignments.Where(a => a.RoleId == id).Select(a => a.EntityId).Distinct()
            .ToListAsync(ct);
        await ApplyAsync(role, req, assignedAt, ct);
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        await access.EnsureAtRootAsync(Permissions.RolesManage, ct); // roles apply organization-wide
        var role = await db.Roles.FirstOrDefaultAsync(r => r.Id == id, ct) ?? throw new NotFoundException("Role");
        if (role.IsSystem) throw new ValidationException("Default roles can be edited but not deleted.");
        if (await db.UserRoleAssignments.AnyAsync(a => a.RoleId == id, ct))
            throw new ValidationException("Remove this role from all users before deleting it.");
        role.IsDeleted = true;
        await db.SaveChangesAsync(ct);
    }

    private async Task ApplyAsync(Role role, SaveRoleRequest req, List<Guid> assignedAt, CancellationToken ct)
    {
        var name = Guard.Required(req.Name, "Name", 100);
        if (await db.Roles.AnyAsync(r => r.Name == name && r.Id != role.Id, ct))
            throw new ValidationException($"A role named '{name}' already exists.");

        var unknown = req.Permissions.Where(p => !Permissions.Exists(p)).ToList();
        if (unknown.Count > 0) throw new ValidationException($"Unknown permissions: {string.Join(", ", unknown)}");

        var current = role.Permissions.Select(p => p.PermissionCode).ToHashSet();
        var wanted = req.Permissions.ToHashSet();
        var added = wanted.Except(current).ToList();

        // Editing a role changes the rights of everyone who has it, so a non-owner may only add
        // permissions they hold at every entity the role is assigned at (or somewhere, if unassigned).
        if (!currentUser.IsSuperAdmin && added.Count > 0)
        {
            var mine = await access.CurrentAsync(ct);
            var blocked = added.Where(p => assignedAt.Count == 0
                ? !mine.HasAnywhere(p)
                : assignedAt.Any(e => !mine.Has(p, e))).ToList();
            if (blocked.Count > 0)
                throw new ForbiddenException($"You can't grant permissions you don't hold: {string.Join(", ", blocked.Take(5))}");
        }

        role.Name = name;
        role.Description = req.Description;
        foreach (var rp in role.Permissions.Where(p => !wanted.Contains(p.PermissionCode)).ToList())
            role.Permissions.Remove(rp);
        foreach (var code in added)
            role.Permissions.Add(new RolePermission { RoleId = role.Id, PermissionCode = code });
    }

    private IQueryable<RoleDto> Project(IQueryable<Role> q) =>
        q.OrderByDescending(r => r.IsSystem).ThenBy(r => r.Name).Select(r => new RoleDto(r.Id, r.Name, r.Description,
            r.IsSystem, r.Permissions.Select(p => p.PermissionCode).ToList(),
            db.UserRoleAssignments.Count(a => a.RoleId == r.Id)));
}
