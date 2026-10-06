using Erpos.Application.Common;
using Erpos.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Erpos.Application.Authorization;

/// <summary>Map of entityId → permission codes the user holds at that entity.</summary>
public sealed class EffectivePermissions(Dictionary<Guid, HashSet<string>> map)
{
    public IReadOnlyDictionary<Guid, HashSet<string>> ByEntity => map;

    public bool Has(string permission, Guid entityId) =>
        map.TryGetValue(entityId, out var set) && set.Contains(permission);

    public bool HasAnywhere(string permission) => map.Values.Any(s => s.Contains(permission));

    public HashSet<Guid> EntitiesWith(string permission) =>
        map.Where(kv => kv.Value.Contains(permission)).Select(kv => kv.Key).ToHashSet();
}

public interface IAccessService
{
    /// <summary>Effective permissions of the signed-in user, computed once per request.</summary>
    Task<EffectivePermissions> CurrentAsync(CancellationToken ct = default);

    /// <summary>Effective permissions of any user in the current tenant (for the admin UI).</summary>
    Task<EffectivePermissions> ForUserAsync(Guid userId, CancellationToken ct = default);

    Task<bool> HasAsync(string permission, Guid entityId, CancellationToken ct = default);
    Task EnsureAsync(string permission, Guid entityId, CancellationToken ct = default);
    Task EnsureAnywhereAsync(string permission, CancellationToken ct = default);
    Task<HashSet<Guid>> EntitiesWithAsync(string permission, CancellationToken ct = default);

    /// <summary>Forget the cached permissions after changing entities, modules or assignments in this request.</summary>
    void Invalidate();

    /// <summary>Organization-wide settings require the permission at the root entity.</summary>
    Task EnsureAtRootAsync(string permission, CancellationToken ct = default);
}

public class AccessService(IAppDbContext db, ICurrentUser currentUser) : IAccessService
{
    private EffectivePermissions? _current;

    public async Task<EffectivePermissions> CurrentAsync(CancellationToken ct = default)
    {
        if (_current != null) return _current;
        if (currentUser.UserId is not { } userId || currentUser.TenantId is null)
            return _current = new EffectivePermissions([]);
        return _current = await ComputeAsync(userId, currentUser.UserType!.Value, ct);
    }

    public async Task<EffectivePermissions> ForUserAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await db.Users.Where(u => u.Id == userId).Select(u => new { u.UserType }).FirstOrDefaultAsync(ct)
                   ?? throw new NotFoundException("User");
        return await ComputeAsync(userId, user.UserType, ct);
    }

    public async Task<bool> HasAsync(string permission, Guid entityId, CancellationToken ct = default) =>
        (await CurrentAsync(ct)).Has(permission, entityId);

    public async Task EnsureAsync(string permission, Guid entityId, CancellationToken ct = default)
    {
        if (!await HasAsync(permission, entityId, ct))
            throw new ForbiddenException($"Missing permission '{permission}' for this entity.");
    }

    public async Task EnsureAnywhereAsync(string permission, CancellationToken ct = default)
    {
        if (!(await CurrentAsync(ct)).HasAnywhere(permission))
            throw new ForbiddenException($"Missing permission '{permission}'.");
    }

    public void Invalidate() => _current = null;

    public async Task EnsureAtRootAsync(string permission, CancellationToken ct = default)
    {
        var rootId = await db.Entities.Where(e => e.ParentId == null).Select(e => e.Id).FirstOrDefaultAsync(ct);
        await EnsureAsync(permission, rootId, ct);
    }

    public async Task<HashSet<Guid>> EntitiesWithAsync(string permission, CancellationToken ct = default) =>
        (await CurrentAsync(ct)).EntitiesWith(permission);

    private async Task<EffectivePermissions> ComputeAsync(Guid userId, UserType userType, CancellationToken ct)
    {
        // Tenant query filters on the DbContext keep all of this inside the caller's organization.
        var entities = await db.Entities.Select(e => new { e.Id, e.Path }).ToListAsync(ct);
        var pathOf = entities.ToDictionary(e => e.Id, e => e.Path);

        var modulesByEntity = (await db.EntityModules.Where(m => m.IsEnabled)
                .Select(m => new { m.EntityId, m.ModuleCode }).ToListAsync(ct))
            .GroupBy(m => m.EntityId)
            .ToDictionary(g => g.Key, g => g.Select(m => m.ModuleCode).ToHashSet());

        bool ModuleOn(Guid entityId, string permission)
        {
            var module = Permissions.ModuleOf(permission);
            return Modules.IsAlwaysOn(module) ||
                   (modulesByEntity.TryGetValue(entityId, out var set) && set.Contains(module));
        }

        var result = new Dictionary<Guid, HashSet<string>>();

        if (userType == UserType.SuperAdmin)
        {
            foreach (var e in entities)
                result[e.Id] = Permissions.Catalog.Select(p => p.Code).Where(c => ModuleOn(e.Id, c)).ToHashSet();
            return new EffectivePermissions(result);
        }

        var assignments = await db.UserRoleAssignments.Where(a => a.UserId == userId)
            .Select(a => new { a.RoleId, a.EntityId, a.IncludeDescendants }).ToListAsync(ct);
        var roleIds = assignments.Select(a => a.RoleId).Distinct().ToList();
        var permsByRole = (await db.RolePermissions
                .Where(rp => roleIds.Contains(rp.RoleId) && db.Roles.Any(r => r.Id == rp.RoleId))
                .ToListAsync(ct))
            .GroupBy(rp => rp.RoleId)
            .ToDictionary(g => g.Key, g => g.Select(rp => rp.PermissionCode).ToList());

        var overrides = await db.UserPermissionOverrides.Where(o => o.UserId == userId)
            .Select(o => new { o.EntityId, o.PermissionCode, o.IsGranted, o.IncludeDescendants }).ToListAsync(ct);

        bool Covers(Guid sourceEntity, bool includeDescendants, Guid targetEntity, string targetPath) =>
            sourceEntity == targetEntity ||
            (includeDescendants && pathOf.TryGetValue(sourceEntity, out var p) && targetPath.StartsWith(p));

        foreach (var e in entities)
        {
            var set = new HashSet<string>();
            foreach (var a in assignments.Where(a => Covers(a.EntityId, a.IncludeDescendants, e.Id, e.Path)))
                if (permsByRole.TryGetValue(a.RoleId, out var codes))
                    set.UnionWith(codes);

            var applicable = overrides.Where(o => Covers(o.EntityId, o.IncludeDescendants, e.Id, e.Path)).ToList();
            set.UnionWith(applicable.Where(o => o.IsGranted).Select(o => o.PermissionCode));
            set.ExceptWith(applicable.Where(o => !o.IsGranted).Select(o => o.PermissionCode));
            set.RemoveWhere(c => !ModuleOn(e.Id, c));

            if (set.Count > 0) result[e.Id] = set;
        }

        return new EffectivePermissions(result);
    }
}
