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
    public async Task<AuthResponse> LoginAsync(LoginRequest req, CancellationToken ct)
    {
        var email = req.Email.Trim().ToLowerInvariant();
        // No tenant is known before login, so bypass the tenant filter for this lookup only.
        var user = await db.Users.IgnoreQueryFilters().Include(u => u.Tenant)
            .FirstOrDefaultAsync(u => u.Email == email && !u.IsDeleted, ct);

        if (user == null || !hasher.Verify(req.Password, user.PasswordHash)) throw new UnauthorizedException();
        if (!user.IsActive) throw new UnauthorizedException("This account is disabled.");
        if (user.Tenant is { Status: TenantStatus.Suspended })
            throw new UnauthorizedException("Your organization's account is suspended.");

        user.LastLoginAt = DateTime.UtcNow;
        return await IssueAsync(user, ct);
    }

    public async Task<AuthResponse> RefreshAsync(RefreshRequest req, CancellationToken ct)
    {
        var hash = tokens.HashToken(req.RefreshToken);
        var stored = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (stored == null || stored.RevokedAt != null || stored.ExpiresAt < DateTime.UtcNow)
            throw new UnauthorizedException("Session expired. Please sign in again.");

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
