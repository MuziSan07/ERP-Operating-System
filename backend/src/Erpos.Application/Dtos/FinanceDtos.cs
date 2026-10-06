using Erpos.Domain.Enums;

namespace Erpos.Application.Dtos;

// ---- Setup ----
public record AccountDto(Guid Id, string Code, string Name, AccountType Type, AccountSubType SubType, Guid? ParentId, bool IsGroup,
    bool IsSystem, string? Currency, string? Description, bool IsActive);
public record SaveAccountRequest(string Code, string Name, AccountType Type, AccountSubType SubType, Guid? ParentId, bool IsGroup,
    string? Currency, string? Description, bool IsActive);

public record FinanceSettingsDto(string BaseCurrency, int FiscalYearStartMonth, DateOnly? LockedThrough, string? Ntn, string? Strn,
    Guid? ReceivableAccountId, Guid? PayableAccountId, Guid? RetainedEarningsAccountId, Guid? ExchangeGainLossAccountId,
    Guid? SalaryExpenseAccountId, Guid? SalaryPayableAccountId, Guid? SalaryTaxPayableAccountId, Guid? EobiExpenseAccountId,
    Guid? EobiPayableAccountId, Guid? PfExpenseAccountId, Guid? PfPayableAccountId, Guid? SocialSecurityExpenseAccountId,
    Guid? SocialSecurityPayableAccountId, Guid? OtherPayrollDeductionsAccountId, Guid? DefaultInventoryAccountId,
    Guid? DefaultConsumptionAccountId, Guid? GrniAccountId, Guid? PriceVarianceAccountId, Guid? InventoryAdjustmentAccountId,
    decimal PriceTolerance, Guid? CustomerAdvanceAccountId = null);

public record ExchangeRateDto(Guid Id, string Currency, DateOnly Date, decimal Rate);
public record SaveExchangeRateRequest(string Currency, DateOnly Date, decimal Rate);

public record TaxRateDto(Guid Id, string Code, string Name, decimal Rate, string? Authority, Guid? OutputAccountId, Guid? InputAccountId, bool IsActive);
public record SaveTaxRateRequest(string Code, string Name, decimal Rate, string? Authority, Guid? OutputAccountId, Guid? InputAccountId, bool IsActive);

public record ContactDto(Guid Id, string Code, string Name, bool IsCustomer, bool IsVendor, string? Email, string? Phone,
    string? Address, string? City, string? Country, string? Ntn, string? Strn, string? Cnic, string? Currency,
    int PaymentTermsDays, bool IsActive, decimal Receivable, decimal Payable, Guid? DefaultWhtRateId = null, bool NotOnActiveTaxpayerList = false);
public record SaveContactRequest(string Code, string Name, bool IsCustomer, bool IsVendor, string? Email, string? Phone,
    string? Address, string? City, string? Country, string? Ntn, string? Strn, string? Cnic, string? Currency,
    int PaymentTermsDays, bool IsActive, Guid? DefaultWhtRateId = null, bool NotOnActiveTaxpayerList = false);

// ---- Journals ----
public record JournalLineDto(Guid AccountId, string AccountCode, string AccountName, Guid EntityId, string? EntityName,
    string? Description, decimal Debit, decimal Credit, decimal BaseDebit, decimal BaseCredit, Guid? ContactId, string? ContactName);
public record JournalEntryDto(Guid Id, string? Number, Guid EntityId, string EntityName, DateOnly Date, string? Reference,
    string Description, JournalStatus Status, JournalSource Source, Guid? SourceId, string Currency, decimal ExchangeRate,
    decimal Total, decimal BaseTotal, string? CreatedByName, string? PostedByName, DateTime? PostedAt, Guid? ReversalOfId,
    Guid? ReversedById, IReadOnlyList<JournalLineDto> Lines);
public record JournalLineInput(Guid AccountId, decimal Debit, decimal Credit, Guid? EntityId, string? Description, Guid? ContactId);
public record SaveJournalRequest(Guid EntityId, DateOnly Date, string? Reference, string Description, string? Currency,
    decimal? ExchangeRate, List<JournalLineInput> Lines);
public record ReverseRequest(DateOnly? Date, string? Reason);

// ---- Invoices & bills ----
public record DocumentLineDto(Guid Id, string Description, Guid AccountId, string AccountName, decimal Quantity, decimal UnitPrice,
    decimal Amount, Guid? TaxRateId, string? TaxRateName, decimal TaxRate, decimal TaxAmount, Guid? PurchaseOrderLineId);
public record DocumentDto(Guid Id, DocumentKind Kind, string? Number, Guid EntityId, string EntityName, Guid ContactId,
    string ContactName, string? ContactNtn, string? ContactStrn, string? ContactAddress, DateOnly Date, DateOnly DueDate,
    string? Reference, string? Notes, string Currency, decimal ExchangeRate, DocumentStatus Status, decimal Subtotal,
    decimal TaxTotal, decimal Total, decimal AmountPaid, decimal Balance, decimal BaseTotal, Guid? JournalEntryId,
    DateTime CreatedAt, Guid? PurchaseOrderId, string? PurchaseOrderNumber, IReadOnlyList<DocumentLineDto> Lines);
public record DocumentListItem(Guid Id, DocumentKind Kind, string? Number, string EntityName, Guid ContactId, string ContactName,
    DateOnly Date, DateOnly DueDate, string? Reference, string Currency, DocumentStatus Status, decimal Total, decimal Balance,
    int DaysOverdue, decimal Subtotal = 0, Guid EntityId = default);
public record DocumentLineInput(string Description, Guid AccountId, decimal Quantity, decimal UnitPrice, Guid? TaxRateId,
    Guid? PurchaseOrderLineId = null);
public record SaveDocumentRequest(Guid EntityId, Guid ContactId, DateOnly Date, DateOnly? DueDate, string? Reference, string? Notes,
    string? Currency, decimal? ExchangeRate, List<DocumentLineInput> Lines);

// ---- Payments ----
public record AllocationDto(Guid DocumentId, string? DocumentNumber, decimal Amount, decimal BaseAmount);
public record PaymentDto(Guid Id, PaymentKind Kind, string Number, Guid EntityId, string EntityName, Guid ContactId, string ContactName,
    DateOnly Date, Guid BankAccountId, string BankAccountName, string Currency, decimal ExchangeRate, decimal Amount,
    string? Reference, string? Notes, bool IsVoid, Guid? JournalEntryId, IReadOnlyList<AllocationDto> Allocations,
    decimal WithholdingTax = 0, decimal NetPaid = 0, string? WithholdingSection = null);
public record AllocationInput(Guid DocumentId, decimal Amount);
public record CreatePaymentRequest(PaymentKind Kind, Guid EntityId, Guid ContactId, DateOnly Date, Guid BankAccountId,
    string? Currency, decimal? ExchangeRate, decimal Amount, string? Reference, string? Notes, List<AllocationInput> Allocations,
    Guid? WithholdingTaxRateId = null);
public record PaySalariesRequest(Guid BankAccountId, DateOnly Date);

// ---- Reports ----
public record TrialBalanceRow(Guid AccountId, string Code, string Name, AccountType Type, decimal Debit, decimal Credit);
public record TrialBalanceDto(DateOnly AsOf, string Currency, IReadOnlyList<TrialBalanceRow> Rows, decimal TotalDebit, decimal TotalCredit);
public record StatementLine(string Code, string Name, decimal Amount, bool IsTotal = false, int Level = 1);
public record StatementSection(string Title, IReadOnlyList<StatementLine> Lines, decimal Total);
public record FinancialStatementDto(string Title, DateOnly From, DateOnly To, string Currency, IReadOnlyList<StatementSection> Sections,
    decimal Result, string ResultLabel);
public record LedgerRow(DateOnly Date, string? Number, Guid JournalEntryId, string Description, string? EntityName, string? ContactName,
    decimal Debit, decimal Credit, decimal Balance);
public record GeneralLedgerDto(Guid AccountId, string Code, string Name, DateOnly From, DateOnly To, decimal Opening,
    IReadOnlyList<LedgerRow> Rows, decimal Closing);
public record AgingRow(Guid ContactId, string ContactName, decimal Current, decimal Days1To30, decimal Days31To60, decimal Days61To90,
    decimal Over90, decimal Total);
public record AgingDto(DocumentKind Kind, DateOnly AsOf, string Currency, IReadOnlyList<AgingRow> Rows, AgingRow Totals);
public record SalesTaxRow(Guid TaxRateId, string Code, string Name, string? Authority, decimal Rate, decimal SalesValue,
    decimal OutputTax, decimal PurchaseValue, decimal InputTax, decimal Net);
public record SalesTaxReportDto(DateOnly From, DateOnly To, string Currency, IReadOnlyList<SalesTaxRow> Rows, decimal TotalOutput,
    decimal TotalInput, decimal NetPayable);
public record FinanceDashboardDto(string Currency, decimal CashAndBank, decimal Receivables, decimal Payables, decimal OverdueReceivables,
    decimal IncomeThisYear, decimal ExpensesThisYear, decimal ProfitThisYear, IReadOnlyList<MonthlyPoint> Monthly);
public record MonthlyPoint(int Year, int Month, decimal Income, decimal Expenses);
