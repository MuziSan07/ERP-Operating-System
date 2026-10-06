using Erpos.Domain.Common;
using Erpos.Domain.Enums;

namespace Erpos.Domain.Entities;

public class Department : BaseEntity, IEntityScoped
{
    public Guid TenantId { get; set; }
    public Guid EntityId { get; set; }
    public BusinessEntity? Entity { get; set; }
    public string Name { get; set; } = "";
    public string Code { get; set; } = "";
    public Guid? HeadEmployeeId { get; set; }
    public Employee? HeadEmployee { get; set; }
}

public class Designation : BaseEntity, ITenantOwned
{
    public Guid TenantId { get; set; }
    public string Title { get; set; } = "";
    public string? Grade { get; set; }
}

/// <summary>
/// HR record of a person. Every employee has a login (<see cref="User"/>); name, email and phone live on the user.
/// <see cref="EntityId"/> always equals the user's PrimaryEntityId.
/// </summary>
public class Employee : BaseEntity, IEntityScoped
{
    public Guid TenantId { get; set; }
    public Guid EntityId { get; set; }
    public BusinessEntity? Entity { get; set; }
    public Guid UserId { get; set; }
    public User? User { get; set; }

    public string EmployeeCode { get; set; } = "";
    public Guid? DepartmentId { get; set; }
    public Department? Department { get; set; }
    public Guid? DesignationId { get; set; }
    public Designation? Designation { get; set; }
    /// <summary>Line manager; first approver of leave by default.</summary>
    public Guid? ManagerId { get; set; }
    public Employee? Manager { get; set; }

    public EmploymentType EmploymentType { get; set; } = EmploymentType.Permanent;
    public EmployeeStatus Status { get; set; } = EmployeeStatus.Active;
    public DateOnly JoinDate { get; set; }
    public DateOnly? ConfirmationDate { get; set; }
    public DateOnly? ExitDate { get; set; }

    public string? FatherName { get; set; }
    public string? Cnic { get; set; }
    public Gender? Gender { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? EmergencyContactName { get; set; }
    public string? EmergencyContactPhone { get; set; }

    public string? BankName { get; set; }
    public string? BankAccountTitle { get; set; }
    public string? Iban { get; set; }
    public string? Ntn { get; set; }
    public string? EobiNumber { get; set; }
    public bool EobiMember { get; set; } = true;
    public bool ProvidentFundMember { get; set; }
    public bool SocialSecurityMember { get; set; }
}

public class Holiday : BaseEntity, ITenantOwned
{
    public Guid TenantId { get; set; }
    /// <summary>Null = applies to the whole organization; otherwise to this entity and its sub-entities.</summary>
    public Guid? EntityId { get; set; }
    public DateOnly Date { get; set; }
    public string Name { get; set; } = "";
}

/// <summary>One day's attendance for one employee. Days without a record count as present.</summary>
public class AttendanceRecord : BaseEntity, IEntityScoped
{
    public Guid TenantId { get; set; }
    public Guid EntityId { get; set; }
    public Guid EmployeeId { get; set; }
    public Employee? Employee { get; set; }
    public DateOnly Date { get; set; }
    public AttendanceStatus Status { get; set; }
    public TimeOnly? CheckIn { get; set; }
    public TimeOnly? CheckOut { get; set; }
    public string? Remarks { get; set; }
}

public class LeaveType : BaseEntity, ITenantOwned
{
    public Guid TenantId { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    /// <summary>Yearly entitlement. 0 = no balance tracking (e.g. unpaid leave).</summary>
    public decimal DaysPerYear { get; set; }
    public bool IsPaid { get; set; } = true;
    public bool AllowHalfDay { get; set; } = true;
    public Gender? OnlyForGender { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>Ordered approval chain applied to every leave request in the organization.</summary>
public class LeaveApprovalStep : ITenantOwned
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public int StepOrder { get; set; }
    public string Name { get; set; } = "";
    public ApproverType ApproverType { get; set; }
    public Guid? ApproverUserId { get; set; }
}

public class LeaveRequest : BaseEntity, IEntityScoped
{
    public Guid TenantId { get; set; }
    public Guid EntityId { get; set; }
    public Guid EmployeeId { get; set; }
    public Employee? Employee { get; set; }
    public Guid LeaveTypeId { get; set; }
    public LeaveType? LeaveType { get; set; }
    public DateOnly FromDate { get; set; }
    public DateOnly ToDate { get; set; }
    public bool IsHalfDay { get; set; }
    /// <summary>Working days (weekly offs and holidays excluded).</summary>
    public decimal Days { get; set; }
    public string? Reason { get; set; }
    public LeaveStatus Status { get; set; } = LeaveStatus.Pending;
    public ICollection<LeaveApproval> Approvals { get; set; } = new List<LeaveApproval>();
}

public class LeaveApproval
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid LeaveRequestId { get; set; }
    public int StepOrder { get; set; }
    public string StepName { get; set; } = "";
    public ApproverType ApproverType { get; set; }
    /// <summary>Resolved approver for LineManager / DepartmentHead / SpecificUser steps.</summary>
    public Guid? ApproverUserId { get; set; }
    public ApprovalStatus Status { get; set; }
    public Guid? ActedByUserId { get; set; }
    public DateTime? ActedAt { get; set; }
    public string? Comment { get; set; }
}

/// <summary>Organization-wide HR and payroll settings (one row per organization).</summary>
public class HrSettings : ITenantOwned
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    /// <summary>Comma-separated DayOfWeek names, e.g. "Sunday" or "Saturday,Sunday".</summary>
    public string WeeklyOffDays { get; set; } = "Sunday";

    public decimal MinimumWage { get; set; }
    public bool EobiEnabled { get; set; } = true;
    public decimal EobiEmployeeRate { get; set; }
    public decimal EobiEmployerRate { get; set; }
    public bool ProvidentFundEnabled { get; set; }
    public decimal ProvidentFundEmployeeRate { get; set; }
    public decimal ProvidentFundEmployerRate { get; set; }
    public bool SocialSecurityEnabled { get; set; }
    public decimal SocialSecurityEmployerRate { get; set; }
    public decimal SocialSecurityWageCeiling { get; set; }
    /// <summary>The approver of a payroll run must be a different person from its creator.</summary>
    public bool PayrollRequiresSecondApprover { get; set; } = true;
}
