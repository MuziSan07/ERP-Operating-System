using Erpos.Application.Common;
using Erpos.Domain.Enums;
using Erpos.Infrastructure.Security;

namespace Erpos.Api.Infrastructure;

public class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private System.Security.Claims.ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public Guid? UserId => Guid.TryParse(Principal?.FindFirst("sub")?.Value, out var id) ? id : null;
    public Guid? TenantId => Guid.TryParse(Principal?.FindFirst(ErposClaims.TenantId)?.Value, out var id) ? id : null;
    public UserType? UserType =>
        Enum.TryParse<UserType>(Principal?.FindFirst(ErposClaims.UserType)?.Value, out var t) ? t : null;
}
