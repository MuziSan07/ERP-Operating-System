using Erpos.Domain.Common;
using Erpos.Domain.Enums;

namespace Erpos.Domain.Entities;

/// <summary>A client engagement (software build, retainer, internal work). The client is a finance customer.</summary>
public class Project : BaseEntity, IEntityScoped
{
    public Guid TenantId { get; set; }
    public Guid EntityId { get; set; }
    public BusinessEntity? Entity { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public Guid? ClientId { get; set; }
    public Contact? Client { get; set; }
    public ProjectBillingType BillingType { get; set; } = ProjectBillingType.TimeAndMaterials;
    public ProjectStatus Status { get; set; } = ProjectStatus.Planned;
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public decimal BudgetHours { get; set; }
    /// <summary>Fixed-price contract value (milestones add up to it).</summary>
    public decimal ContractAmount { get; set; }
    /// <summary>Hourly rate when a member has none of their own.</summary>
    public decimal DefaultBillRate { get; set; }
    public Guid? TaxRateId { get; set; }
    public Guid? ManagerEmployeeId { get; set; }
    public string? Description { get; set; }
    public int NextTaskNumber { get; set; } = 1;
    public ICollection<ProjectMember> Members { get; set; } = new List<ProjectMember>();
    public ICollection<ProjectMilestone> Milestones { get; set; } = new List<ProjectMilestone>();
}

public class ProjectMember
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public Guid EmployeeId { get; set; }
    public Employee? Employee { get; set; }
    public string? Role { get; set; }
    /// <summary>Hourly rate charged to the client (null: project default).</summary>
    public decimal? BillRate { get; set; }
    /// <summary>Hourly cost to the company (from salary unless overridden).</summary>
    public decimal CostRate { get; set; }
}

public class ProjectMilestone
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public string Name { get; set; } = "";
    public DateOnly DueDate { get; set; }
    public decimal Amount { get; set; }
    public DateOnly? CompletedOn { get; set; }
    public Guid? InvoiceId { get; set; }
    public int SortOrder { get; set; }
}

public class ProjectTask : BaseEntity, ITenantOwned
{
    public Guid TenantId { get; set; }
    public Guid ProjectId { get; set; }
    public Project? Project { get; set; }
    public int Number { get; set; }
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public WorkItemStatus Status { get; set; } = WorkItemStatus.Todo;
    public WorkItemPriority Priority { get; set; } = WorkItemPriority.Medium;
    public Guid? AssigneeEmployeeId { get; set; }
    public Employee? Assignee { get; set; }
    public decimal EstimateHours { get; set; }
    public DateOnly? DueDate { get; set; }
    public Guid? MilestoneId { get; set; }
    public int SortOrder { get; set; }
    public DateTime? CompletedAt { get; set; }
}

/// <summary>Hours an employee spent on a project; submitted weekly, approved by a project approver, then invoiced.</summary>
public class TimeEntry : BaseEntity, IEntityScoped
{
    public Guid TenantId { get; set; }
    /// <summary>The project's entity (approval and reporting follow the project).</summary>
    public Guid EntityId { get; set; }
    public Guid EmployeeId { get; set; }
    public Employee? Employee { get; set; }
    public Guid ProjectId { get; set; }
    public Project? Project { get; set; }
    public Guid? TaskId { get; set; }
    public ProjectTask? Task { get; set; }
    public DateOnly Date { get; set; }
    public decimal Hours { get; set; }
    public string? Description { get; set; }
    public bool Billable { get; set; }
    public TimeEntryStatus Status { get; set; } = TimeEntryStatus.Draft;
    public decimal BillRate { get; set; }
    public decimal CostRate { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public Guid? ApprovedByUserId { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? RejectReason { get; set; }
    public Guid? InvoiceId { get; set; }
}
