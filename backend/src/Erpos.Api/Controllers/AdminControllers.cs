using Erpos.Application.Authorization;
using Erpos.Application.Dtos;
using Erpos.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Erpos.Api.Controllers;

// Authorization beyond "signed in" lives in the services (entity-scoped permission checks),
// so controllers stay thin and the same rules apply whatever calls the service.

[ApiController, Authorize, Route("api/platform/tenants")]
public class TenantsController(TenantService tenants) : ControllerBase
{
    [HttpGet] public Task<List<TenantDto>> List(CancellationToken ct) => tenants.ListAsync(ct);
    [HttpPost] public Task<TenantDto> Create(CreateTenantRequest req, CancellationToken ct) => tenants.CreateAsync(req, ct);
    [HttpPut("{id:guid}")]
    public Task<TenantDto> Update(Guid id, UpdateTenantRequest req, CancellationToken ct) => tenants.UpdateAsync(id, req, ct);
}

[ApiController, Authorize, Route("api/entities")]
public class EntitiesController(EntityService entities) : ControllerBase
{
    [HttpGet] public Task<List<EntityDto>> List(CancellationToken ct) => entities.ListAsync(ct);
    [HttpGet("{id:guid}")] public Task<EntityDto> Get(Guid id, CancellationToken ct) => entities.GetAsync(id, ct);
    [HttpPost] public Task<EntityDto> Create(CreateEntityRequest req, CancellationToken ct) => entities.CreateAsync(req, ct);
    [HttpPut("{id:guid}")]
    public Task<EntityDto> Update(Guid id, SaveEntityRequest req, CancellationToken ct) => entities.UpdateAsync(id, req, ct);
    [HttpPost("{id:guid}/move")]
    public Task<EntityDto> Move(Guid id, MoveEntityRequest req, CancellationToken ct) => entities.MoveAsync(id, req, ct);

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await entities.DeleteAsync(id, ct);
        return NoContent();
    }

    [HttpGet("{id:guid}/modules")]
    public Task<List<ModuleStateDto>> Modules(Guid id, CancellationToken ct) => entities.GetModulesAsync(id, ct);
    [HttpPut("{id:guid}/modules")]
    public Task<List<ModuleStateDto>> SetModules(Guid id, SetModulesRequest req, CancellationToken ct) =>
        entities.SetModulesAsync(id, req, ct);
}

[ApiController, Authorize, Route("api/users")]
public class UsersController(UserService users) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<UserDto>> List([FromQuery] Guid? entityId, [FromQuery] bool includeSubEntities = true,
        [FromQuery] string? search = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 25,
        CancellationToken ct = default) =>
        users.ListAsync(entityId, includeSubEntities, search, page, pageSize, ct);

    [HttpGet("{id:guid}")] public Task<UserDto> Get(Guid id, CancellationToken ct) => users.GetAsync(id, ct);
    [HttpPost] public Task<UserDto> Create(CreateUserRequest req, CancellationToken ct) => users.CreateAsync(req, ct);
    [HttpPut("{id:guid}")]
    public Task<UserDto> Update(Guid id, UpdateUserRequest req, CancellationToken ct) => users.UpdateAsync(id, req, ct);

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await users.DeleteAsync(id, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/reset-password")]
    public async Task<IActionResult> ResetPassword(Guid id, ResetPasswordRequest req, CancellationToken ct)
    {
        await users.ResetPasswordAsync(id, req, ct);
        return NoContent();
    }

    [HttpGet("{id:guid}/access")]
    public Task<UserAccessDto> Access(Guid id, CancellationToken ct) => users.GetAccessAsync(id, ct);
    [HttpPost("{id:guid}/assignments")]
    public Task<UserAccessDto> AddAssignment(Guid id, CreateAssignmentRequest req, CancellationToken ct) =>
        users.AddAssignmentAsync(id, req, ct);
    [HttpDelete("{id:guid}/assignments/{assignmentId:guid}")]
    public Task<UserAccessDto> RemoveAssignment(Guid id, Guid assignmentId, CancellationToken ct) =>
        users.RemoveAssignmentAsync(id, assignmentId, ct);
    [HttpPost("{id:guid}/overrides")]
    public Task<UserAccessDto> AddOverride(Guid id, CreateOverrideRequest req, CancellationToken ct) =>
        users.AddOverrideAsync(id, req, ct);
    [HttpDelete("{id:guid}/overrides/{overrideId:guid}")]
    public Task<UserAccessDto> RemoveOverride(Guid id, Guid overrideId, CancellationToken ct) =>
        users.RemoveOverrideAsync(id, overrideId, ct);
}

[ApiController, Authorize, Route("api/roles")]
public class RolesController(RoleService roles) : ControllerBase
{
    [HttpGet] public Task<List<RoleDto>> List(CancellationToken ct) => roles.ListAsync(ct);
    [HttpGet("{id:guid}")] public Task<RoleDto> Get(Guid id, CancellationToken ct) => roles.GetAsync(id, ct);
    [HttpPost] public Task<RoleDto> Create(SaveRoleRequest req, CancellationToken ct) => roles.CreateAsync(req, ct);
    [HttpPut("{id:guid}")]
    public Task<RoleDto> Update(Guid id, SaveRoleRequest req, CancellationToken ct) => roles.UpdateAsync(id, req, ct);

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await roles.DeleteAsync(id, ct);
        return NoContent();
    }
}

[ApiController, Authorize, Route("api/catalog")]
public class CatalogController : ControllerBase
{
    [HttpGet("permissions")] public List<PermissionGroupDto> Permissions() => RoleService.Catalog();
    [HttpGet("modules")] public IReadOnlyList<ModuleDef> ModuleList() => Application.Authorization.Modules.Catalog;
}

[ApiController, Authorize, Route("api")]
public class InsightsController(InsightService insights) : ControllerBase
{
    [HttpGet("dashboard")] public Task<DashboardDto> Dashboard(CancellationToken ct) => insights.DashboardAsync(ct);

    [HttpGet("audit")]
    public Task<PagedResult<AuditLogDto>> Audit([FromQuery] string? table, [FromQuery] Guid? userId,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default) =>
        insights.AuditAsync(table, userId, page, pageSize, ct);
}
