namespace Erpos.Domain.Common;

public abstract class BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; }
}

/// <summary>Rows that belong to exactly one organization. Filtered automatically by the DbContext.</summary>
public interface ITenantOwned
{
    Guid TenantId { get; set; }
}

/// <summary>Rows that belong to a node in the organization's entity tree.</summary>
public interface IEntityScoped : ITenantOwned
{
    Guid EntityId { get; set; }
}
