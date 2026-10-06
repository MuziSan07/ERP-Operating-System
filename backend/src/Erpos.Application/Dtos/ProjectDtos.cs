using Erpos.Domain.Enums;

namespace Erpos.Application.Dtos;

// ---- People & clients ----
public record ProjectPerson(Guid EmployeeId, string Name, string EmployeeCode, string? Designation, string EntityName, decimal CostRate);
public record ClientDto(Guid Id, string Code, string Name, string? Email, string? Phone, string? City, string? Ntn, int ActiveProjects, decimal Billed, decimal Unbilled);
public record SaveClientRequest(string Name, string? Email, string? Phone, string? Address, string? City, string? Country, string? Ntn, string? Strn, int PaymentTermsDays);

// ---- Projects ----
public record MemberInput(Guid? Id, Guid EmployeeId, string? Role, decimal? BillRate, decimal? CostRate);
public record MilestoneInput(Guid? Id, string Name, DateOnly DueDate, decimal Amount);
public record SaveProjectRequest(Guid EntityId, string Code, string Name, Guid? ClientId, ProjectBillingType BillingType, DateOnly StartDate, DateOnly? EndDate,
    decimal BudgetHours, decimal ContractAmount, decimal DefaultBillRate, Guid? TaxRateId, Guid? ManagerEmployeeId, string? Description,
    List<MemberInput> Members, List<MilestoneInput> Milestones);
public record ProjectListItem(Guid Id, string Code, string Name, string? ClientName, ProjectBillingType BillingType, ProjectStatus Status, DateOnly StartDate,
    DateOnly? EndDate, string? ManagerName, decimal BudgetHours, decimal HoursLogged, decimal HoursPercent, decimal Billed, decimal Unbilled, decimal Cost,
    decimal Margin, int OpenTasks, bool OverBudget);
public record MemberDto(Guid Id, Guid EmployeeId, string Name, string? Role, decimal? BillRate, decimal EffectiveBillRate, decimal CostRate, decimal Hours, decimal BillableHours);
public record MilestoneDto(Guid Id, string Name, DateOnly DueDate, decimal Amount, DateOnly? CompletedOn, Guid? InvoiceId, string? InvoiceNumber, bool Overdue);
public record ProjectDto(Guid Id, Guid EntityId, string EntityName, string Code, string Name, Guid? ClientId, string? ClientName, ProjectBillingType BillingType,
    ProjectStatus Status, DateOnly StartDate, DateOnly? EndDate, decimal BudgetHours, decimal ContractAmount, decimal DefaultBillRate, Guid? TaxRateId,
    Guid? ManagerEmployeeId, string? ManagerName, string? Description, IReadOnlyList<MemberDto> Members, IReadOnlyList<MilestoneDto> Milestones,
    decimal HoursLogged, decimal HoursApproved, decimal HoursPending, decimal BillableHours, decimal Billed, decimal Unbilled, decimal Cost, decimal Margin,
    decimal MarginPercent, decimal HoursPercent, IReadOnlyDictionary<string, int> TasksByStatus, IReadOnlyList<string> Warnings);

// ---- Tasks ----
public record TaskDto(Guid Id, Guid ProjectId, string Key, string Title, string? Description, WorkItemStatus Status, WorkItemPriority Priority,
    Guid? AssigneeEmployeeId, string? AssigneeName, decimal EstimateHours, decimal LoggedHours, DateOnly? DueDate, Guid? MilestoneId, int SortOrder, bool Overdue);
public record SaveTaskRequest(Guid ProjectId, string Title, string? Description, WorkItemStatus Status, WorkItemPriority Priority, Guid? AssigneeEmployeeId,
    decimal EstimateHours, DateOnly? DueDate, Guid? MilestoneId);
public record MoveTaskRequest(WorkItemStatus Status, int SortOrder);

// ---- Timesheets ----
public record TimeEntryDto(Guid Id, Guid EmployeeId, string EmployeeName, Guid ProjectId, string ProjectCode, string ProjectName, Guid? TaskId, string? TaskKey,
    string? TaskTitle, DateOnly Date, decimal Hours, string? Description, bool Billable, TimeEntryStatus Status, decimal BillRate, string? RejectReason,
    string? ApprovedByName);
public record SaveTimeEntryRequest(Guid ProjectId, Guid? TaskId, DateOnly Date, decimal Hours, string? Description, bool Billable);
public record TimesheetProject(Guid Id, string Code, string Name, bool Billable, IReadOnlyList<TaskOption> Tasks);
public record TaskOption(Guid Id, string Key, string Title);
public record MyWeekDto(DateOnly WeekStart, DateOnly WeekEnd, Guid? EmployeeId, IReadOnlyList<TimeEntryDto> Entries, IReadOnlyList<TimesheetProject> Projects,
    decimal TotalHours, decimal BillableHours, bool CanSubmit);
public record DecideTimeRequest(List<Guid> EntryIds, string? Reason);
public record InvoiceProjectRequest(DateOnly? UpTo, DateOnly? InvoiceDate);
public record ProjectInvoiceResult(Guid InvoiceId, string? InvoiceNumber, decimal Hours, decimal Total, int Entries);

// ---- Reports ----
public record UtilizationRow(Guid EmployeeId, string Name, string? Designation, decimal Capacity, decimal Hours, decimal BillableHours, decimal Utilization,
    decimal BillableValue);
public record UtilizationDto(DateOnly From, DateOnly To, IReadOnlyList<UtilizationRow> Rows, decimal Capacity, decimal Hours, decimal BillableHours, decimal Utilization);
public record ProjectsDashboardDto(int ActiveProjects, decimal HoursThisWeek, decimal BillableThisWeek, decimal Unbilled, decimal BilledThisMonth,
    int PendingApprovals, IReadOnlyList<ProjectListItem> AtRisk, IReadOnlyList<MilestoneDue> MilestonesDue, IReadOnlyList<TaskDto> OverdueTasks);
public record MilestoneDue(Guid ProjectId, string ProjectCode, string Name, DateOnly DueDate, decimal Amount, bool Completed, bool Overdue);
