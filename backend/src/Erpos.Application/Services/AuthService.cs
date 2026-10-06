using Erpos.Application.Authorization;
using Erpos.Application.Common;
using Erpos.Application.Dtos;
using Erpos.Domain.Entities;
using Erpos.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Erpos.Application.Services;

public class AuthService(IAppDbContext db, IPasswordHasher hasher, ITokenService tokens, ICurrentUser currentUser,
    IAccessService access)
{
    private const int MaxFailedLogins = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);
    // Checked against when the email is unknown, so both cases take the same BCrypt time (no account enumeration).
    private static string? _dummyHash;

    public async Task<AuthResponse> LoginAsync(LoginRequest req, CancellationToken ct)
    {
        var email = req.Email.Trim().ToLowerInvariant();
        // No tenant is known before login, so bypass the tenant filter for this lookup only.
        var user = await db.Users.IgnoreQueryFilters().Include(u => u.Tenant)
            .FirstOrDefaultAsync(u => u.Email == email && !u.IsDeleted, ct);

        if (user == null)
        {
            hasher.Verify(req.Password, _dummyHash ??= hasher.Hash(Guid.NewGuid().ToString()));
            throw new UnauthorizedException();
        }
        var now = DateTime.UtcNow;
        if (user.LockedUntil > now)
            throw new UnauthorizedException($"Too many failed attempts. The account is locked for {Math.Ceiling((user.LockedUntil.Value - now).TotalMinutes)} more minute(s).");
        if (!hasher.Verify(req.Password, user.PasswordHash))
        {
            // Saved outside any request transaction (the login endpoint opts out), so the count survives the 401.
            if (++user.FailedLoginCount >= MaxFailedLogins)
            {
                user.FailedLoginCount = 0;
                user.LockedUntil = now + LockoutDuration;
            }
            await db.SaveChangesAsync(ct);
            throw new UnauthorizedException();
        }
        if (!user.IsActive) throw new UnauthorizedException("This account is disabled.");
        if (user.Tenant is { Status: TenantStatus.Suspended })
            throw new UnauthorizedException("Your organization's account is suspended.");

        user.LastLoginAt = now;
        user.FailedLoginCount = 0;
        user.LockedUntil = null;
        return await IssueAsync(user, ct);
    }

    public async Task<AuthResponse> RefreshAsync(RefreshRequest req, CancellationToken ct)
    {
        var hash = tokens.HashToken(req.RefreshToken);
        var stored = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (stored == null || stored.ExpiresAt < DateTime.UtcNow)
            throw new UnauthorizedException("Session expired. Please sign in again.");
        if (stored.RevokedAt != null)
        {
            // A rotated (already used) token came back: someone else has a copy. End every session of this user.
            await UserRules.RevokeSessionsAsync(db, stored.UserId, ct);
            await db.SaveChangesAsync(ct);
            throw new UnauthorizedException("This session was ended for your security. Please sign in again.");
        }

        var user = await db.Users.IgnoreQueryFilters().Include(u => u.Tenant)
            .FirstOrDefaultAsync(u => u.Id == stored.UserId && !u.IsDeleted && u.IsActive, ct);
        if (user == null || user.Tenant is { Status: TenantStatus.Suspended }) throw new UnauthorizedException();

        stored.RevokedAt = DateTime.UtcNow; // rotate
        return await IssueAsync(user, ct);
    }

    public async Task LogoutAsync(RefreshRequest req, CancellationToken ct)
    {
        var hash = tokens.HashToken(req.RefreshToken);
        var stored = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (stored is { RevokedAt: null })
        {
            stored.RevokedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }
    }

    public async Task<MeResponse> MeAsync(CancellationToken ct)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedException();
        var user = await db.Users.IgnoreQueryFilters().Include(u => u.Tenant).Include(u => u.PrimaryEntity)
            .FirstOrDefaultAsync(u => u.Id == userId, ct) ?? throw new UnauthorizedException();

        var entities = new List<EntityAccessDto>();
        if (user.TenantId != null)
        {
            var perms = await access.CurrentAsync(ct);
            var ids = perms.ByEntity.Keys.ToList();
            var rows = await db.Entities.Where(e => ids.Contains(e.Id)).OrderBy(e => e.Depth).ThenBy(e => e.Name)
                .Select(e => new
                {
                    e.Id, e.ParentId, e.Name, e.Code, e.Industry, e.Depth, e.IsActive,
                    Modules = e.Modules.Where(m => m.IsEnabled).Select(m => m.ModuleCode).ToList()
                }).ToListAsync(ct);
            entities = rows.Select(e => new EntityAccessDto(e.Id, e.ParentId, e.Name, e.Code, e.Industry, e.Depth,
                e.IsActive, [Modules.Core, .. e.Modules], perms.ByEntity[e.Id].OrderBy(c => c).ToList())).ToList();
        }

        return new MeResponse(user.ToDto(), user.Tenant?.ToDto(0, 0), entities);
    }

    public async Task ChangePasswordAsync(ChangePasswordRequest req, CancellationToken ct)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedException();
        var user = await db.Users.IgnoreQueryFilters().FirstAsync(u => u.Id == userId, ct);
        if (!hasher.Verify(req.CurrentPassword, user.PasswordHash))
            throw new ValidationException("Current password is incorrect.");
        Guard.Password(req.NewPassword);
        user.PasswordHash = hasher.Hash(req.NewPassword);
        await UserRules.RevokeSessionsAsync(db, user.Id, ct); // a thief holding an old session is logged out too
        await db.SaveChangesAsync(ct);
    }

    private async Task<AuthResponse> IssueAsync(User user, CancellationToken ct)
    {
        var (access, expires) = tokens.CreateAccessToken(user);
        var refresh = tokens.CreateRefreshToken();
        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = tokens.HashToken(refresh),
            ExpiresAt = DateTime.UtcNow.AddDays(tokens.RefreshTokenDays)
        });
        await db.SaveChangesAsync(ct);
        return new AuthResponse(access, expires, refresh, user.ToDto());
    }
}
