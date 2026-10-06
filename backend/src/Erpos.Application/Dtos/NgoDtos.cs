using Erpos.Domain.Enums;

namespace Erpos.Application.Dtos;

// ---- Donors, funds, programs ----
public record DonorDto(Guid Id, Guid ContactId, string Code, string Name, DonorType Type, string? Email, string? Phone, string? Cnic, string? Ntn,
    string? Address, string? City, string? Country, string? Notes, decimal TotalDonated, int Donations, DateOnly? LastDonation, int Grants, decimal GrantsReceived);
public record SaveDonorRequest(string Name, DonorType Type, string? Email, string? Phone, string? Cnic, string? Ntn, string? Address, string? City,
    string? Country, string? Notes);
public record FundDto(Guid Id, string Code, string Name, FundKind Kind, string? Purpose, Guid? GrantId, string? GrantNumber, bool IsActive,
    decimal Received, decimal Spent, decimal Balance);
public record SaveFundRequest(string Code, string Name, FundKind Kind, string? Purpose, bool IsActive);
public record ProgramDto(Guid Id, Guid EntityId, string EntityName, string Code, string Name, string? Sector, string? Description, int TargetBeneficiaries,
    bool IsActive, int Beneficiaries, decimal Spent);
public record SaveProgramRequest(Guid EntityId, string Code, string Name, string? Sector, string? Description, int TargetBeneficiaries, bool IsActive);

// ---- Grants ----
public record BudgetLineInput(Guid? Id, string Code, string Description, BudgetCategory Category, decimal Amount, Guid? ExpenseAccountId);
public record TrancheInput(Guid? Id, DateOnly DueDate, decimal Amount, string? Condition);
public record SaveGrantRequest(Guid EntityId, string Title, Guid DonorId, string? AgreementRef, Guid? ProgramId, string? Currency, decimal? AgreementRate,
    DateOnly StartDate, DateOnly EndDate, ReportingFrequency ReportingFrequency, decimal FlexibilityPercent, string? Notes,
    List<BudgetLineInput> BudgetLines, List<TrancheInput> Tranches);
public record GrantListItem(Guid Id, string Number, string Title, string DonorName, string Currency, decimal Amount, DateOnly StartDate, DateOnly EndDate,
    GrantStatus Status, decimal ReceivedBase, decimal SpentBase, decimal BurnPercent, decimal TimeElapsedPercent, DateOnly? NextReportDue);
public record BudgetLineDto(Guid Id, string Code, string Description, BudgetCategory Category, decimal Amount, Guid? ExpenseAccountId, string? ExpenseAccountName,
    decimal ActualBase, decimal Actual, decimal Variance, decimal BurnPercent, bool OverBudget);
public record TrancheDto(Guid Id, int Sequence, DateOnly DueDate, decimal Amount, string? Condition, DateOnly? ReceivedDate, decimal? ReceivedAmount,
    decimal? ReceivedBase, bool Overdue);
public record GrantReportDto(Guid Id, string Title, DateOnly PeriodStart, DateOnly PeriodEnd, DateOnly DueDate, DateOnly? SubmittedOn, string? Notes, bool Overdue);
public record FundExpenseDto(Guid Id, string Number, DateOnly Date, string FundCode, string? GrantNumber, string? BudgetLineCode, string? ProgramName,
    FunctionalCategory Function, string AccountName, string Description, decimal Amount, string PaidHow, Guid? BillId, Guid? AssistanceId);
public record GrantDto(Guid Id, string Number, Guid EntityId, string EntityName, string Title, Guid DonorId, string DonorName, string? AgreementRef,
    Guid? ProgramId, string? ProgramName, Guid FundId, string FundCode, string Currency, decimal AgreementRate, DateOnly StartDate, DateOnly EndDate,
    GrantStatus Status, ReportingFrequency ReportingFrequency, decimal FlexibilityPercent, string? Notes, decimal Amount, decimal ReceivedAmount,
    decimal ReceivedBase, decimal SpentBase, decimal Spent, decimal AverageRate, decimal UnspentBase, decimal BurnPercent, decimal TimeElapsedPercent,
    IReadOnlyList<BudgetLineDto> BudgetLines, IReadOnlyList<TrancheDto> Tranches, IReadOnlyList<GrantReportDto> Reports,
    IReadOnlyList<FundExpenseDto> Expenses, IReadOnlyList<string> Warnings);
public record ReceiveTrancheRequest(Guid TrancheId, DateOnly Date, decimal Amount, Guid BankAccountId, decimal? Rate);
public record SubmitReportRequest(DateOnly? SubmittedOn, string? Notes);
public record CloseGrantRequest(DateOnly? Date, Guid? RefundFromAccountId);

// ---- Donations & expenses ----
public record RecordDonationRequest(Guid EntityId, DateOnly Date, Guid DonorId, Guid FundId, decimal Amount, DonationMethod Method, string? Reference,
    Guid BankAccountId, Guid? ProgramId, string? Notes);
public record DonationDto(Guid Id, string Number, Guid EntityId, string EntityName, DateOnly Date, Guid DonorId, string DonorName, string? DonorCnic,
    string? DonorNtn, string? DonorAddress, Guid FundId, string FundName, FundKind FundKind, decimal Amount, string AmountInWords, DonationMethod Method,
    string? Reference, string BankAccountName, string? ProgramName, string? Notes, string OrganizationName);
public record DonationListItem(Guid Id, string Number, DateOnly Date, string DonorName, string FundCode, FundKind FundKind, decimal Amount, DonationMethod Method,
    string? Reference);
public record ChargeExpenseRequest(Guid? EntityId, DateOnly Date, Guid? FundId, Guid? GrantId, Guid? BudgetLineId, Guid? ProgramId, FunctionalCategory Function,
    Guid? AccountId, string Description, decimal Amount, Guid? PaidFromAccountId, Guid? VendorId, bool AllocationOnly);

// ---- Beneficiaries ----
public record BeneficiaryListItem(Guid Id, string RegistrationNo, string FullName, string? Cnic, Gender Gender, string? District, int HouseholdSize,
    string? ProgramName, bool ZakatEligible, bool IsActive, DateOnly EnrolledOn, int AssistanceCount, decimal AssistanceValue, DateOnly? LastAssisted);
public record AssistanceDto(Guid Id, DateOnly Date, AssistanceType Type, string Description, decimal? Quantity, decimal Value, string? ProgramName,
    string? FundCode, string? ExpenseNumber);
public record BeneficiaryDto(Guid Id, Guid EntityId, string EntityName, string RegistrationNo, string FullName, string? Cnic, Gender Gender, DateOnly? DateOfBirth,
    int? Age, string? Phone, string? District, string? Address, int HouseholdSize, string? Vulnerabilities, bool ZakatEligible, Guid? ProgramId,
    string? ProgramName, DateOnly EnrolledOn, bool IsActive, IReadOnlyList<AssistanceDto> Assistance, decimal AssistanceValue);
public record SaveBeneficiaryRequest(Guid EntityId, string FullName, string? Cnic, Gender Gender, DateOnly? DateOfBirth, string? Phone, string? District,
    string? Address, int HouseholdSize, string? Vulnerabilities, bool ZakatEligible, Guid? ProgramId, DateOnly? EnrolledOn, bool IsActive);
public record AddAssistanceRequest(DateOnly Date, AssistanceType Type, string Description, decimal? Quantity, decimal Value, Guid? ProgramId,
    Guid? FundId, Guid? GrantId, Guid? BudgetLineId, Guid? PaidFromAccountId, bool AllowRepeat);

// ---- Reports ----
public record FunctionalRow(string Program, decimal ProgramCost, decimal ManagementGeneral, decimal Fundraising, decimal Total);
public record FunctionalExpensesDto(DateOnly From, DateOnly To, IReadOnlyList<FunctionalRow> Rows, decimal ProgramCost, decimal ManagementGeneral,
    decimal Fundraising, decimal Total, decimal ProgramRatio);
public record DonorSummaryRow(Guid DonorId, string DonorName, DonorType Type, decimal Donations, int Gifts, decimal GrantsCommitted, decimal GrantsReceived);
public record NgoDashboardDto(decimal DonationsThisMonth, int DonorsThisMonth, int ActiveGrants, decimal CommittedBase, decimal ReceivedBase, decimal SpentBase,
    int ActiveBeneficiaries, int AssistedThisMonth, decimal ProgramRatio, IReadOnlyList<FundDto> Funds, IReadOnlyList<GrantListItem> Grants,
    IReadOnlyList<DueItem> DueSoon);
public record DueItem(string Kind, Guid GrantId, string GrantNumber, string Title, DateOnly DueDate, decimal? Amount, bool Overdue);
