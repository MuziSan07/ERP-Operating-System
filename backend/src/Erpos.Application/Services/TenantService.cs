using Erpos.Application.Authorization;
using Erpos.Application.Common;
using Erpos.Application.Dtos;
using Erpos.Application.Payroll;
using Erpos.Domain.Entities;
using Erpos.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Erpos.Application.Services;

/// <summary>Platform-level organization management. Every method requires a platform administrator.</summary>
public class TenantService(IAppDbContext db, ICurrentUser currentUser, IPasswordHasher hasher)
{
    public async Task<List<TenantDto>> ListAsync(CancellationToken ct)
    {
        EnsurePlatform();
        var rows = await db.Tenants.IgnoreQueryFilters().Where(t => !t.IsDeleted).OrderBy(t => t.Name)
            .Select(t => new
            {
                Tenant = t,
                Users = db.Users.IgnoreQueryFilters().Count(u => u.TenantId == t.Id && !u.IsDeleted),
                Entities = db.Entities.IgnoreQueryFilters().Count(e => e.TenantId == t.Id && !e.IsDeleted)
            }).ToListAsync(ct);
        return rows.Select(r => r.Tenant.ToDto(r.Users, r.Entities)).ToList();
    }

    public async Task<TenantDto> CreateAsync(CreateTenantRequest req, CancellationToken ct)
    {
        EnsurePlatform();
        var name = Guard.Required(req.Name, "Name");
        var code = Guard.Code(req.Code);
        var email = Guard.Email(req.SuperAdminEmail);
        var adminName = Guard.Required(req.SuperAdminName, "Super admin name");
        Guard.Password(req.SuperAdminPassword);

        if (await db.Tenants.IgnoreQueryFilters().AnyAsync(t => t.Code == code, ct))
            throw new ValidationException($"Organization code '{code}' is already used.");
        if (await db.Users.IgnoreQueryFilters().AnyAsync(u => u.Email == email && !u.IsDeleted, ct))
            throw new ValidationException($"Email '{email}' is already registered.");

        var tenant = new Tenant
        {
            Name = name, Code = code, ContactEmail = req.ContactEmail, ContactPhone = req.ContactPhone,
            Country = req.Country, MaxUsers = req.MaxUsers > 0 ? req.MaxUsers : 50
        };

        var root = new BusinessEntity
        {
            TenantId = tenant.Id, Name = name, Code = code, Industry = req.Industry, Depth = 0,
            Currency = string.IsNullOrWhiteSpace(req.Currency) ? "USD" : req.Currency,
            TimeZone = string.IsNullOrWhiteSpace(req.TimeZone) ? "UTC" : req.TimeZone, Country = req.Country,
            Email = req.ContactEmail, Phone = req.ContactPhone
        };
        root.Path = BusinessEntity.BuildPath(null, root.Id);

        var modules = req.Modules is { Count: > 0 }
            ? req.Modules.Where(Modules.Exists)
            : Modules.Catalog.Where(m => m.SuggestedFor.Contains(req.Industry)).Select(m => m.Code);
        foreach (var m in modules.Where(m => !Modules.IsAlwaysOn(m)).Distinct())
            root.Modules.Add(new EntityModule { TenantId = tenant.Id, EntityId = root.Id, ModuleCode = m });

        var superAdmin = new User
        {
            TenantId = tenant.Id, Email = email, FullName = adminName, UserType = UserType.SuperAdmin,
            PasswordHash = hasher.Hash(req.SuperAdminPassword), PrimaryEntityId = root.Id
        };

        db.Tenants.Add(tenant);
        db.Entities.Add(root);
        db.Roles.AddRange(DefaultRoles.Create(tenant.Id));
        db.Users.Add(superAdmin);
        await HrDefaults.EnsureAsync(db, tenant.Id, ct);
        await Finance.FinanceDefaults.EnsureAsync(db, tenant.Id, ct, root.Currency);
        await db.SaveChangesAsync(ct);

        return tenant.ToDto(1, 1);
    }

    public async Task<TenantDto> UpdateAsync(Guid id, UpdateTenantRequest req, CancellationToken ct)
    {
        EnsurePlatform();
        var t = await db.Tenants.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct)
                ?? throw new NotFoundException("Organization");
        t.Name = Guard.Required(req.Name, "Name");
        t.Status = req.Status;
        t.ContactEmail = req.ContactEmail;
        t.ContactPhone = req.ContactPhone;
        t.Country = req.Country;
        t.MaxUsers = req.MaxUsers > 0 ? req.MaxUsers : t.MaxUsers;
        await db.SaveChangesAsync(ct);
        return t.ToDto(0, 0);
    }

    private void EnsurePlatform()
    {
        if (!currentUser.IsPlatformAdmin) throw new ForbiddenException("Platform administrator access required.");
    }
}
