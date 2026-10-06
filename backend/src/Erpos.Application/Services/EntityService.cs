using Erpos.Application.Authorization;
using Erpos.Application.Common;
using Erpos.Application.Dtos;
using Erpos.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Erpos.Application.Services;

public class EntityService(IAppDbContext db, IAccessService access, ICurrentUser currentUser)
{
    /// <summary>Every entity the user may view, flat. The client builds the tree from ParentId.</summary>
    public async Task<List<EntityDto>> ListAsync(CancellationToken ct)
    {
        var ids = await access.EntitiesWithAsync(Permissions.EntitiesView, ct);
        return await Project(db.Entities.Where(e => ids.Contains(e.Id))).ToListAsync(ct);
    }

    public async Task<EntityDto> GetAsync(Guid id, CancellationToken ct)
    {
        await access.EnsureAsync(Permissions.EntitiesView, id, ct);
        return await Project(db.Entities.Where(e => e.Id == id)).FirstOrDefaultAsync(ct)
               ?? throw new NotFoundException("Entity");
    }

    public async Task<EntityDto> CreateAsync(CreateEntityRequest req, CancellationToken ct)
    {
        await access.EnsureAsync(Permissions.EntitiesCreate, req.ParentId, ct);
        var parent = await db.Entities.Include(e => e.Modules).FirstOrDefaultAsync(e => e.Id == req.ParentId, ct)
                     ?? throw new NotFoundException("Parent entity");

        var entity = new BusinessEntity
        {
            TenantId = parent.TenantId, ParentId = parent.Id, Depth = parent.Depth + 1,
            Currency = parent.Currency, TimeZone = parent.TimeZone
        };
        entity.Path = BusinessEntity.BuildPath(parent.Path, entity.Id);
        await ApplyAsync(entity, req.Data, ct);

        // A child can only use modules its parent has. Default: inherit everything the parent has.
        var parentModules = parent.Modules.Where(m => m.IsEnabled).Select(m => m.ModuleCode).ToHashSet();
        var wanted = req.Modules ?? parentModules.ToList();
        foreach (var m in wanted.Where(parentModules.Contains).Distinct())
            entity.Modules.Add(new EntityModule { TenantId = entity.TenantId, EntityId = entity.Id, ModuleCode = m });

        db.Entities.Add(entity);
        await db.SaveChangesAsync(ct);
        access.Invalidate();
        return await GetAsync(entity.Id, ct);
    }

    public async Task<EntityDto> UpdateAsync(Guid id, SaveEntityRequest req, CancellationToken ct)
    {
        await access.EnsureAsync(Permissions.EntitiesEdit, id, ct);
        var entity = await db.Entities.FirstOrDefaultAsync(e => e.Id == id, ct) ?? throw new NotFoundException("Entity");
        await ApplyAsync(entity, req, ct);
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<EntityDto> MoveAsync(Guid id, MoveEntityRequest req, CancellationToken ct)
    {
        await access.EnsureAsync(Permissions.EntitiesEdit, id, ct);
        await access.EnsureAsync(Permissions.EntitiesCreate, req.NewParentId, ct);

        var entity = await db.Entities.FirstOrDefaultAsync(e => e.Id == id, ct) ?? throw new NotFoundException("Entity");
        if (entity.ParentId == null) throw new ValidationException("The organization root cannot be moved.");
        var newParent = await db.Entities.Include(e => e.Modules).FirstOrDefaultAsync(e => e.Id == req.NewParentId, ct)
                        ?? throw new NotFoundException("New parent");
        if (newParent.Path.StartsWith(entity.Path))
            throw new ValidationException("An entity cannot be moved under itself or its own sub-entity.");

        var oldPath = entity.Path;
        var newPath = BusinessEntity.BuildPath(newParent.Path, entity.Id);
        var depthShift = newParent.Depth + 1 - entity.Depth;
        var subtree = await db.Entities.Include(e => e.Modules).Where(e => e.Path.StartsWith(oldPath)).ToListAsync(ct);
        var allowed = newParent.Modules.Where(m => m.IsEnabled).Select(m => m.ModuleCode).ToHashSet();

        foreach (var e in subtree)
        {
            e.Path = newPath + e.Path[oldPath.Length..];
            e.Depth += depthShift;
            foreach (var m in e.Modules.Where(m => m.IsEnabled && !allowed.Contains(m.ModuleCode)))
                m.IsEnabled = false;
        }
        entity.ParentId = newParent.Id;
        await db.SaveChangesAsync(ct);
        access.Invalidate();
        return await GetAsync(id, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        await access.EnsureAsync(Permissions.EntitiesDelete, id, ct);
        var entity = await db.Entities.FirstOrDefaultAsync(e => e.Id == id, ct) ?? throw new NotFoundException("Entity");
        if (entity.ParentId == null) throw new ValidationException("The organization root cannot be deleted.");
        if (await db.Entities.AnyAsync(e => e.ParentId == id, ct))
            throw new ValidationException("Delete or move its sub-entities first.");
        if (await db.Users.AnyAsync(u => u.PrimaryEntityId == id, ct))
            throw new ValidationException("Move the users that belong to this entity first.");
        if (await db.Departments.AnyAsync(d => d.EntityId == id, ct))
            throw new ValidationException("Delete or move the departments of this entity first.");
        if (await db.PayrollRuns.AnyAsync(r => r.EntityId == id, ct))
            throw new ValidationException("This entity has payroll history and can only be deactivated.");

        entity.IsDeleted = true;
        db.UserRoleAssignments.RemoveRange(await db.UserRoleAssignments.Where(a => a.EntityId == id).ToListAsync(ct));
        db.UserPermissionOverrides.RemoveRange(await db.UserPermissionOverrides.Where(o => o.EntityId == id).ToListAsync(ct));
        await db.SaveChangesAsync(ct);
    }

    public async Task<List<ModuleStateDto>> GetModulesAsync(Guid id, CancellationToken ct)
    {
        await access.EnsureAsync(Permissions.EntitiesView, id, ct);
        var entity = await db.Entities.Include(e => e.Modules).FirstOrDefaultAsync(e => e.Id == id, ct)
                     ?? throw new NotFoundException("Entity");
        var available = await AvailableModulesAsync(entity, ct);
        var enabled = entity.Modules.Where(m => m.IsEnabled).Select(m => m.ModuleCode).ToHashSet();

        return Modules.Catalog.Select(m => new ModuleStateDto(m.Code, m.Name, m.Description, m.AlwaysOn,
            m.AlwaysOn || enabled.Contains(m.Code), m.AlwaysOn || available.Contains(m.Code),
            m.SuggestedFor.Contains(entity.Industry))).ToList();
    }

    public async Task<List<ModuleStateDto>> SetModulesAsync(Guid id, SetModulesRequest req, CancellationToken ct)
    {
        await access.EnsureAsync(Permissions.ModulesManage, id, ct);
        var entity = await db.Entities.Include(e => e.Modules).FirstOrDefaultAsync(e => e.Id == id, ct)
                     ?? throw new NotFoundException("Entity");
        var available = await AvailableModulesAsync(entity, ct);
        var wanted = req.Modules.Where(m => Modules.Exists(m) && !Modules.IsAlwaysOn(m)).ToHashSet();

        var notAllowed = wanted.Where(m => !available.Contains(m)).ToList();
        if (notAllowed.Count > 0)
            throw new ValidationException($"Enable these on the parent entity first: {string.Join(", ", notAllowed)}.");

        foreach (var code in wanted)
        {
            var row = entity.Modules.FirstOrDefault(m => m.ModuleCode == code);
            if (row == null)
                db.EntityModules.Add(new EntityModule { TenantId = entity.TenantId, EntityId = entity.Id, ModuleCode = code });
            else row.IsEnabled = true;
        }

        // Turning a module off here turns it off for the whole branch below.
        var disabled = entity.Modules.Where(m => m.IsEnabled && !wanted.Contains(m.ModuleCode))
            .Select(m => m.ModuleCode).ToList();
        if (disabled.Count > 0)
        {
            var branch = await db.EntityModules
                .Where(m => m.IsEnabled && disabled.Contains(m.ModuleCode) && m.Entity!.Path.StartsWith(entity.Path))
                .ToListAsync(ct);
            foreach (var m in branch) m.IsEnabled = false;
        }

        await db.SaveChangesAsync(ct);
        access.Invalidate();
        return await GetModulesAsync(id, ct);
    }

    private async Task<HashSet<string>> AvailableModulesAsync(BusinessEntity entity, CancellationToken ct)
    {
        if (entity.ParentId == null)
        {
            // Only the organization owner decides which modules the organization uses at the top.
            return currentUser.IsSuperAdmin
                ? Modules.Catalog.Select(m => m.Code).ToHashSet()
                : entity.Modules.Where(m => m.IsEnabled).Select(m => m.ModuleCode).ToHashSet();
        }
        return (await db.EntityModules.Where(m => m.EntityId == entity.ParentId && m.IsEnabled)
            .Select(m => m.ModuleCode).ToListAsync(ct)).ToHashSet();
    }

    private async Task ApplyAsync(BusinessEntity e, SaveEntityRequest r, CancellationToken ct)
    {
        var code = Guard.Code(r.Code);
        if (await db.Entities.AnyAsync(x => x.Code == code && x.Id != e.Id, ct))
            throw new ValidationException($"Entity code '{code}' is already used in this organization.");

        e.Name = Guard.Required(r.Name, "Name");
        e.Code = code;
        e.Industry = r.Industry;
        e.IsActive = r.IsActive;
        e.Email = r.Email;
        e.Phone = r.Phone;
        e.Address = r.Address;
        e.City = r.City;
        e.Country = r.Country;
        e.Currency = string.IsNullOrWhiteSpace(r.Currency) ? e.Currency : r.Currency.Trim().ToUpperInvariant();
        e.TimeZone = string.IsNullOrWhiteSpace(r.TimeZone) ? e.TimeZone : r.TimeZone.Trim();
        e.TaxNumber = r.TaxNumber;
    }

    private IQueryable<EntityDto> Project(IQueryable<BusinessEntity> q) =>
        q.OrderBy(e => e.Depth).ThenBy(e => e.Name).Select(e => new EntityDto(e.Id, e.ParentId, e.Name, e.Code,
            e.Industry, e.Depth, e.IsActive, e.Email, e.Phone, e.Address, e.City, e.Country, e.Currency, e.TimeZone,
            e.TaxNumber,
            db.Entities.Count(c => c.ParentId == e.Id),
            db.Users.Count(u => u.PrimaryEntityId == e.Id),
            e.Modules.Where(m => m.IsEnabled).Select(m => m.ModuleCode).ToList()));
}
