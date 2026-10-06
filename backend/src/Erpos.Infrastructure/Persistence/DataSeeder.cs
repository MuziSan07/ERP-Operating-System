using Erpos.Application.Common;
using Erpos.Application.Authorization;
using Erpos.Application.Finance;
using Erpos.Application.Payroll;
using Erpos.Domain.Entities;
using Erpos.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Erpos.Infrastructure.Persistence;

/// <summary>Applies migrations and makes sure the first platform administrator exists.</summary>
public class DataSeeder(AppDbContext db, IPasswordHasher hasher, IConfiguration config, ILogger<DataSeeder> logger)
{
    public async Task RunAsync(CancellationToken ct = default)
    {
        await db.Database.MigrateAsync(ct);

        // Organizations created before HR & Payroll existed get the default leave types, tax tables, etc.
        foreach (var tenantId in await db.Tenants.IgnoreQueryFilters().Where(t => !t.IsDeleted).Select(t => t.Id).ToListAsync(ct))
        {
            await HrDefaults.EnsureAsync(db, tenantId, ct);
            await FinanceDefaults.EnsureAsync(db, tenantId, ct);
            await FinanceDefaults.UpgradeAsync(db, tenantId, ct);
            var roleNames = await db.Roles.IgnoreQueryFilters().Where(r => r.TenantId == tenantId && !r.IsDeleted)
                .Select(r => r.Name).ToListAsync(ct);
            db.Roles.AddRange(DefaultRoles.Missing(tenantId, roleNames));
        }
        await db.SaveChangesAsync(ct);

        if (await db.Users.IgnoreQueryFilters().AnyAsync(u => u.UserType == UserType.PlatformAdmin, ct)) return;

        var email = config["Seed:PlatformAdminEmail"];
        var password = config["Seed:PlatformAdminPassword"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning("No platform admin exists and Seed:PlatformAdminEmail/Password are not configured.");
            return;
        }

        db.Users.Add(new User
        {
            Email = email.Trim().ToLowerInvariant(),
            FullName = config["Seed:PlatformAdminName"] ?? "Platform Administrator",
            UserType = UserType.PlatformAdmin,
            PasswordHash = hasher.Hash(password)
        });
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Created platform administrator {Email}.", email);
    }
}
