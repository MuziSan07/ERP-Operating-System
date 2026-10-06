using Erpos.Domain.Common;
using Erpos.Domain.Enums;

namespace Erpos.Domain.Entities;

/// <summary>
/// Income tax withheld from suppliers when paying them (Pakistan: section 153 goods / services / contracts).
/// Suppliers not on FBR's Active Taxpayer List suffer twice the rate.
/// </summary>
public class WithholdingTaxRate : BaseEntity, ITenantOwned
{
    public Guid TenantId { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    /// <summary>Legal reference shown on certificates, e.g. "153(1)(b)".</summary>
    public string Section { get; set; } = "";
    /// <summary>Rate for active taxpayers, e.g. 0.09.</summary>
    public decimal Rate { get; set; }
    public Guid PayableAccountId { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>Deducted tax paid into the government treasury for a month (CPR = computerised payment receipt).</summary>
public class WhtDeposit : BaseEntity, IEntityScoped
{
    public Guid TenantId { get; set; }
    public Guid EntityId { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public DateOnly Date { get; set; }
    public decimal Amount { get; set; }
    public string CprNumber { get; set; } = "";
    public Guid BankAccountId { get; set; }
    public Guid? JournalEntryId { get; set; }
}

/// <summary>A bank statement matched against the bank account's ledger lines.</summary>
public class BankReconciliation : BaseEntity, IEntityScoped
{
    public Guid TenantId { get; set; }
    public Guid EntityId { get; set; }
    public Guid BankAccountId { get; set; }
    public Account? BankAccount { get; set; }
    public DateOnly StatementDate { get; set; }
    /// <summary>Closing balance printed on the statement.</summary>
    public decimal StatementBalance { get; set; }
    public ReconciliationStatus Status { get; set; } = ReconciliationStatus.Draft;
    public DateTime? CompletedAt { get; set; }
    public ICollection<BankStatementLine> Lines { get; set; } = new List<BankStatementLine>();
}

public class BankStatementLine
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ReconciliationId { get; set; }
    public DateOnly Date { get; set; }
    public string Description { get; set; } = "";
    public string? Reference { get; set; }
    /// <summary>Money in is positive, money out negative (as the bank sees the account).</summary>
    public decimal Amount { get; set; }
    public Guid? JournalLineId { get; set; }
    public int SortOrder { get; set; }
}
