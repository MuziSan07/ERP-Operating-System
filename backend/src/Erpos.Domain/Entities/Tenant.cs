using Erpos.Domain.Common;
using Erpos.Domain.Enums;

namespace Erpos.Domain.Entities;

/// <summary>An organization subscribed to the platform (NGO, hotel group, logistics company...).</summary>
public class Tenant : BaseEntity
{
    public string Name { get; set; } = "";
    public string Code { get; set; } = "";
    public TenantStatus Status { get; set; } = TenantStatus.Active;
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public string? Country { get; set; }
    public int MaxUsers { get; set; } = 50;

    public ICollection<BusinessEntity> Entities { get; set; } = new List<BusinessEntity>();
}
