using Erpos.Domain.Enums;

namespace Erpos.Application.Dtos;

// ---- Organization structure ----
public record DepartmentDto(Guid Id, Guid EntityId, string EntityName, string Name, string Code, Guid? HeadEmployeeId,
    string? HeadName, int EmployeeCount);
public record SaveDepartmentRequest(Guid EntityId, string Name, string Code, Guid? HeadEmployeeId);
public record DesignationDto(Guid Id, string Title, string? Grade);
public record SaveDesignationRequest(string Title, string? Grade);

// ---- Employees ----
public record EmployeeListItem(Guid Id, string EmployeeCode, Guid UserId, string FullName, string Email, string? Phone,
    Guid EntityId, string EntityName, string? Department, string? Designation, string? ManagerName,
    EmploymentType EmploymentType, EmployeeStatus Status, DateOnly JoinDate);

public record EmployeeDto(Guid Id, string EmployeeCode, Guid UserId, string FullName, string Email, string? Phone,
    UserType UserType, Guid EntityId, string EntityName, Guid? DepartmentId, string? Department, Guid? DesignationId,
    string? Designation, Guid? ManagerId, string? ManagerName, EmploymentType EmploymentType, EmployeeStatus Status,
    DateOnly JoinDate, DateOnly? ConfirmationDate, DateOnly? ExitDate, string? FatherName, string? Cnic, Gender? Gender,
    DateOnly? DateOfBirth, string? Address, string? City, string? EmergencyContactName, string? EmergencyContactPhone,
    string? BankName, string? BankAccountTitle, string? Iban, string? Ntn, string? EobiNumber, bool EobiMember,
    bool ProvidentFundMember, bool SocialSecurityMember);

/// <summary>Fields HR edits on an employee (shared by create and update).</summary>
public record EmployeeData(string EmployeeCode, string FullName, string? Phone, Guid EntityId, Guid? DepartmentId,
    Guid? DesignationId, Guid? ManagerId, EmploymentType EmploymentType, EmployeeStatus Status, DateOnly JoinDate,
    DateOnly? ConfirmationDate, DateOnly? ExitDate, string? FatherName, string? Cnic, Gender? Gender, DateOnly? DateOfBirth,
    string? Address, string? City, string? EmergencyContactName, string? EmergencyContactPhone, string? BankName,
    string? BankAccountTitle, string? Iban, string? Ntn, string? EobiNumber, bool EobiMember, bool ProvidentFundMember,
    bool SocialSecurityMember);

/// <summary>Creates the employee and its login. Without RoleId the default "Employee" role is assigned.</summary>
public record CreateEmployeeRequest(EmployeeData Data, string Email, string Password, UserType UserType, Guid? RoleId,
    decimal? MonthlyGross);

// ---- Attendance ----
public record AttendanceRow(Guid EmployeeId, string EmployeeCode, string FullName, string? Department,
    AttendanceStatus? Status, TimeOnly? CheckIn, TimeOnly? CheckOut, string? Remarks, string? DayType);
public record AttendanceEntry(Guid EmployeeId, AttendanceStatus? Status, TimeOnly? CheckIn, TimeOnly? CheckOut, string? Remarks);
public record SaveAttendanceRequest(DateOnly Date, List<AttendanceEntry> Entries);
public record AttendanceDay(DateOnly Date, AttendanceStatus? Status, string? DayType, TimeOnly? CheckIn, TimeOnly? CheckOut,
    string? Remarks, string? LeaveType);
public record MonthlyAttendanceDto(Guid EmployeeId, string FullName, int Year, int Month, IReadOnlyList<AttendanceDay> Days,
    IReadOnlyDictionary<string, decimal> Summary);
public record HolidayDto(Guid Id, DateOnly Date, string Name, Guid? EntityId, string? EntityName);
public record SaveHolidayRequest(DateOnly Date, string Name, Guid? EntityId);

// ---- Leave ----
public record LeaveTypeDto(Guid Id, string Code, string Name, decimal DaysPerYear, bool IsPaid, bool AllowHalfDay,
    Gender? OnlyForGender, bool IsActive);
public record SaveLeaveTypeRequest(string Code, string Name, decimal DaysPerYear, bool IsPaid, bool AllowHalfDay,
    Gender? OnlyForGender, bool IsActive);
public record LeaveStepDto(Guid Id, int StepOrder, string Name, ApproverType ApproverType, Guid? ApproverUserId, string? ApproverName);
public record SaveLeaveStep(string Name, ApproverType ApproverType, Guid? ApproverUserId);
public record SaveWorkflowRequest(List<SaveLeaveStep> Steps);

public record LeaveBalanceDto(Guid LeaveTypeId, string Code, string Name, bool IsPaid, decimal Entitled, decimal Used,
    decimal Pending, decimal? Available);
public record ApplyLeaveRequest(Guid LeaveTypeId, DateOnly FromDate, DateOnly ToDate, bool IsHalfDay, string? Reason,
    Guid? EmployeeId);
public record LeaveApprovalDto(int StepOrder, string StepName, ApproverType ApproverType, string? ApproverName,
    ApprovalStatus Status, string? ActedBy, DateTime? ActedAt, string? Comment);
public record LeaveRequestDto(Guid Id, Guid EmployeeId, string EmployeeName, string EmployeeCode, string EntityName,
    Guid LeaveTypeId, string LeaveType, bool IsPaid, DateOnly FromDate, DateOnly ToDate, bool IsHalfDay, decimal Days,
    string? Reason, LeaveStatus Status, DateTime CreatedAt, IReadOnlyList<LeaveApprovalDto> Approvals, bool CanAct);
public record LeaveDecisionRequest(bool Approve, string? Comment);

// ---- Settings ----
public record HrSettingsDto(List<string> WeeklyOffDays, decimal MinimumWage, bool EobiEnabled, decimal EobiEmployeeRate,
    decimal EobiEmployerRate, bool ProvidentFundEnabled, decimal ProvidentFundEmployeeRate, decimal ProvidentFundEmployerRate,
    bool SocialSecurityEnabled, decimal SocialSecurityEmployerRate, decimal SocialSecurityWageCeiling,
    bool PayrollRequiresSecondApprover);

// ---- Payroll ----
public record PayComponentDto(Guid Id, string Code, string Name, PayComponentKind Kind, bool IsTaxable, bool IsBasic,
    decimal? ExemptUpToFractionOfBasic, int SortOrder, bool IsActive);
public record SavePayComponentRequest(string Code, string Name, PayComponentKind Kind, bool IsTaxable, bool IsBasic,
    decimal? ExemptUpToFractionOfBasic, int SortOrder, bool IsActive);

public record SalaryLineDto(Guid PayComponentId, string Code, string Name, PayComponentKind Kind, decimal Amount);
public record SalaryDto(Guid Id, DateOnly EffectiveFrom, string? Remarks, decimal Gross, decimal Deductions,
    IReadOnlyList<SalaryLineDto> Lines);
public record SaveSalaryRequest(DateOnly EffectiveFrom, string? Remarks, List<SalaryLineInput> Lines);
public record SalaryLineInput(Guid PayComponentId, decimal Amount);

public record TaxSlabDto(decimal From, decimal? To, decimal FixedTax, decimal Rate);
public record TaxYearDto(Guid Id, int Year, decimal? SurchargeThreshold, decimal SurchargeRate, string? Notes,
    IReadOnlyList<TaxSlabDto> Slabs);
public record SaveTaxYearRequest(int Year, decimal? SurchargeThreshold, decimal SurchargeRate, string? Notes, List<TaxSlabDto> Slabs);
public record TaxPreviewDto(int TaxYear, decimal AnnualIncome, decimal AnnualTax, decimal MonthlyTax, decimal EffectiveRate);

public record CreatePayrollRunRequest(Guid EntityId, int Year, int Month, bool IncludeSubEntities, string? Notes);
public record PayrollRunDto(Guid Id, Guid EntityId, string EntityName, int Year, int Month, bool IncludeSubEntities,
    PayrollRunStatus Status, string? Notes, int EmployeeCount, decimal TotalGross, decimal TotalDeductions, decimal TotalNet,
    decimal TotalEmployerContributions, string? CreatedByName, DateTime CreatedAt, string? ApprovedByName,
    DateTime? ApprovedAt, string? PostedByName, DateTime? PostedAt, Guid? JournalEntryId, Guid? PaymentJournalEntryId,
    IReadOnlyList<string> Warnings);
public record PayslipLineDto(string Code, string Name, PayComponentKind Kind, decimal Amount, bool IsEmployerContribution);
public record PayslipDto(Guid Id, Guid PayrollRunId, PayrollRunStatus RunStatus, Guid EmployeeId, int Year, int Month,
    string EmployeeCode, string EmployeeName, string? Designation, string? Department, string? EntityName, string? Cnic,
    string? Iban, string? BankName, int DaysInMonth, decimal UnpaidDays, decimal PayableDays, decimal MonthlyGross,
    decimal GrossEarnings, decimal TaxableIncome, decimal IncomeTax, decimal TotalDeductions, decimal NetPay,
    decimal EmployerContributions, decimal ProjectedAnnualTaxable, decimal ProjectedAnnualTax,
    IReadOnlyList<PayslipLineDto> Lines);
public record PayrollAdjustmentDto(Guid Id, Guid EmployeeId, string Name, PayComponentKind Kind, decimal Amount, bool IsTaxable);
public record AddAdjustmentRequest(Guid EmployeeId, string Name, PayComponentKind Kind, decimal Amount, bool IsTaxable);
public record PayrollRunDetailDto(PayrollRunDto Run, IReadOnlyList<PayslipDto> Payslips, IReadOnlyList<PayrollAdjustmentDto> Adjustments);

// ---- Self service ----
public record MyHrDto(EmployeeDto? Employee, IReadOnlyList<LeaveBalanceDto> Balances, int PendingApprovals);
