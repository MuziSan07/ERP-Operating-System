using Erpos.Domain.Common;
using Erpos.Domain.Enums;

namespace Erpos.Domain.Entities;

/// <summary>
/// A node in an organization's structure: company, branch, department, project office, hotel property...
/// Nodes nest without limit. <see cref="Path"/> is a materialized path ("/rootId/childId/") so that
/// "this node and everything below it" is a single prefix query.
/// </summary>
public class BusinessEntity : BaseEntity, ITenantOwned
{
    public Guid TenantId { get; set; }
    public Guid? ParentId { get; set; }
    public BusinessEntity? Parent { get; set; }
    public ICollection<BusinessEntity> Children { get; set; } = new List<BusinessEntity>();

    public string Name { get; set; } = "";
    public string Code { get; set; } = "";
    public IndustryType Industry { get; set; } = IndustryType.General;
    public string Path { get; set; } = "";
    public int Depth { get; set; }
    public bool IsActive { get; set; } = true;

    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? Country { get; set; }
    public string Currency { get; set; } = "USD";
    public string TimeZone { get; set; } = "UTC";
    public string? TaxNumber { get; set; }

    public ICollection<EntityModule> Modules { get; set; } = new List<EntityModule>();

    public static string BuildPath(string? parentPath, Guid id) => $"{parentPath ?? "/"}{id}/";
}

/// <summary>A module switched on for one entity.</summary>
public class EntityModule : ITenantOwned
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid EntityId { get; set; }
    public BusinessEntity? Entity { get; set; }
    public string ModuleCode { get; set; } = "";
    public bool IsEnabled { get; set; } = true;
}
