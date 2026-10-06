using Erpos.Application.Authorization;
using Erpos.Application.Common;
using Erpos.Application.Dtos;
using Microsoft.EntityFrameworkCore;

namespace Erpos.Application.Services;

/// <summary>Dashboard numbers and the audit trail.</summary>
public class InsightService(IAppDbContext db, IAccessService access, ICurrentUser currentUser)
{
    public async Task<DashboardDto> DashboardAsync(CancellationToken ct)
    {
        var mine = await access.CurrentAsync(ct);
        var entityIds = mine.EntitiesWith(Permissions.EntitiesView);
        var userEntityIds = mine.EntitiesWith(Permissions.UsersView);

        var entities = await db.Entities.Where(e => entityIds.Contains(e.Id)).Select(e => e.Industry).ToListAsync(ct);
        var users = await db.Users.Where(u => u.PrimaryEntityId != null && userEntityIds.Contains(u.PrimaryEntityId.Value))
            .Select(u => new { u.UserType, u.IsActive }).ToListAsync(ct);
        var roles = mine.HasAnywhere(Permissions.RolesView) ? await db.Roles.CountAsync(ct) : 0;

        return new DashboardDto(entities.Count, users.Count, users.Count(u => u.IsActive), roles,
            users.GroupBy(u => u.UserType.ToString()).ToDictionary(g => g.Key, g => g.Count()),
            entities.GroupBy(i => i.ToString()).ToDictionary(g => g.Key, g => g.Count()));
    }

    /// <summary>The audit trail covers the whole organization, so it needs audit rights at the root entity.</summary>
    public async Task<PagedResult<AuditLogDto>> AuditAsync(string? table, Guid? userId, int page, int pageSize,
        CancellationToken ct)
    {
        var rootId = await db.Entities.Where(e => e.ParentId == null).Select(e => e.Id).FirstOrDefaultAsync(ct);
        await access.EnsureAsync(Permissions.AuditView, rootId, ct);

        var tenantId = currentUser.TenantId;
        var q = db.AuditLogs.Where(a => a.TenantId == tenantId);
        if (!string.IsNullOrWhiteSpace(table)) q = q.Where(a => a.TableName == table);
        if (userId != null) q = q.Where(a => a.UserId == userId);

        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 200);
        var total = await q.CountAsync(ct);
        var rows = await q.OrderByDescending(a => a.Id).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(a => new AuditLogDto(a.Id, a.UserId, db.Users.Where(u => u.Id == a.UserId).Select(u => u.FullName).FirstOrDefault(),
                a.Action, a.TableName, a.RecordId, a.Changes, a.Timestamp))
            .ToListAsync(ct);
        return new PagedResult<AuditLogDto>(rows, total, page, pageSize);
    }
}
