using Erpos.Application.Common;
using Erpos.Domain.Enums;
using Erpos.Infrastructure.Security;

namespace Erpos.Api.Infrastructure;

public class CurrentUser(IHttpContextAccessor accessor, BackgroundTenantContext background) : ICurrentUser
{
    private System.Security.Claims.ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public Guid? UserId => Guid.TryParse(Principal?.FindFirst("sub")?.Value, out var id) ? id : null;
    // Background jobs (reminders) have no request: they set the organization they're working for.
    public Guid? TenantId => Guid.TryParse(Principal?.FindFirst(ErposClaims.TenantId)?.Value, out var id) ? id : accessor.HttpContext == null ? background.TenantId : null;
    public UserType? UserType =>
        Enum.TryParse<UserType>(Principal?.FindFirst(ErposClaims.UserType)?.Value, out var t) ? t : null;
}
