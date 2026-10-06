using Erpos.Domain.Enums;

namespace Erpos.Application.Dtos;

// ---- Withholding tax ----
public record WhtRateDto(Guid Id, string Code, string Name, string Section, decimal Rate, Guid PayableAccountId, string PayableAccountName, bool IsActive);
public record SaveWhtRateRequest(string Code, string Name, string Section, decimal Rate, Guid? PayableAccountId, bool IsActive);
public record WhtDeductionDto(Guid PaymentId, string PaymentNumber, DateOnly Date, Guid VendorId, string VendorName, string? VendorNtn, string? VendorCnic,
    bool NonFiler, string Section, decimal Rate, decimal TaxBase, decimal Tax, string? CprNumber, DateOnly? DepositedOn);
public record WhtSummaryDto(DateOnly From, DateOnly To, IReadOnlyList<WhtDeductionDto> Deductions, decimal TotalBase, decimal TotalTax, decimal Deposited,
    decimal Undeposited);
public record DepositWhtRequest(Guid EntityId, int Year, int Month, DateOnly Date, Guid BankAccountId, string CprNumber);
public record WhtDepositDto(Guid Id, int Year, int Month, DateOnly Date, decimal Amount, string CprNumber, int Deductions);
public record WhtCertificateDto(string OrganizationName, string? OrganizationNtn, string VendorName, string? VendorNtn, string? VendorCnic, DateOnly From,
    DateOnly To, IReadOnlyList<WhtDeductionDto> Deductions, decimal TotalBase, decimal TotalTax);

// ---- Bank reconciliation ----
public record StatementLineInput(DateOnly Date, string Description, string? Reference, decimal Amount);
public record CreateReconciliationRequest(Guid EntityId, Guid BankAccountId, DateOnly StatementDate, decimal StatementBalance,
    List<StatementLineInput>? Lines, string? Csv);
public record StatementLineDto(Guid Id, DateOnly Date, string Description, string? Reference, decimal Amount, Guid? JournalLineId, string? MatchedTo);
public record BookItemDto(Guid JournalLineId, DateOnly Date, string Number, string Description, decimal Amount, bool Matched);
public record ReconciliationListItem(Guid Id, string BankAccountName, DateOnly StatementDate, decimal StatementBalance, ReconciliationStatus Status,
    int Lines, int Unmatched);
public record ReconciliationDto(Guid Id, Guid EntityId, Guid BankAccountId, string BankAccountName, DateOnly StatementDate, decimal StatementBalance,
    ReconciliationStatus Status, IReadOnlyList<StatementLineDto> Lines, IReadOnlyList<BookItemDto> BookItems, decimal BookBalance,
    decimal DepositsInTransit, decimal OutstandingPayments, decimal AdjustedStatementBalance, decimal Difference, int UnmatchedLines, bool CanComplete);
public record MatchRequest(Guid StatementLineId, Guid JournalLineId);
public record UnmatchRequest(Guid StatementLineId);
public record CreateEntryFromLineRequest(Guid StatementLineId, Guid AccountId, string? Description);
public record AutoMatchResult(int Matched, ReconciliationDto Reconciliation);
