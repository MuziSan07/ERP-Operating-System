using Erpos.Domain.Common;
using Erpos.Domain.Enums;

namespace Erpos.Domain.Entities;

/// <summary>Ledger account. Group accounts organize the chart and can't be posted to.</summary>
public class Account : BaseEntity, ITenantOwned
{
    public Guid TenantId { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public AccountType Type { get; set; }
    public AccountSubType SubType { get; set; }
    public Guid? ParentId { get; set; }
    public Account? Parent { get; set; }
    public bool IsGroup { get; set; }
    /// <summary>Default accounts the system posts to; they can be renamed but not deleted.</summary>
    public bool IsSystem { get; set; }
    /// <summary>For bank/cash accounts held in a foreign currency. Null = base currency.</summary>
    public string? Currency { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>Organization-wide accounting settings and the default accounts used for automatic postings.</summary>
public class FinanceSettings : ITenantOwned
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    /// <summary>Functional currency; every ledger amount is also stored in it.</summary>
    public string BaseCurrency { get; set; } = "PKR";
    /// <summary>Pakistan's fiscal year starts in July.</summary>
    public int FiscalYearStartMonth { get; set; } = 7;
    /// <summary>Nothing can be posted on or before this date (closed periods).</summary>
    public DateOnly? LockedThrough { get; set; }
    public string? Ntn { get; set; }
    public string? Strn { get; set; }

    public Guid? ReceivableAccountId { get; set; }
    public Guid? PayableAccountId { get; set; }
    public Guid? RetainedEarningsAccountId { get; set; }
    public Guid? ExchangeGainLossAccountId { get; set; }
    public Guid? SalaryExpenseAccountId { get; set; }
    public Guid? SalaryPayableAccountId { get; set; }
    public Guid? SalaryTaxPayableAccountId { get; set; }
    public Guid? EobiExpenseAccountId { get; set; }
    public Guid? EobiPayableAccountId { get; set; }
    public Guid? PfExpenseAccountId { get; set; }
    public Guid? PfPayableAccountId { get; set; }
    public Guid? SocialSecurityExpenseAccountId { get; set; }
    public Guid? SocialSecurityPayableAccountId { get; set; }
    public Guid? OtherPayrollDeductionsAccountId { get; set; }

    // Inventory & procurement
    public Guid? DefaultInventoryAccountId { get; set; }
    public Guid? DefaultConsumptionAccountId { get; set; }
    /// <summary>Accrual for goods received before the vendor's bill arrives.</summary>
    public Guid? GrniAccountId { get; set; }
    public Guid? PriceVarianceAccountId { get; set; }
    public Guid? InventoryAdjustmentAccountId { get; set; }
    /// <summary>Three-way match: a bill's unit price may differ from the PO by at most this fraction (0.02 = 2%).</summary>
    public decimal PriceTolerance { get; set; } = 0.02m;
    /// <summary>Deposits received before an invoice exists (hotel advances, booking deposits).</summary>
    public Guid? CustomerAdvanceAccountId { get; set; }
}

public class ExchangeRate : BaseEntity, ITenantOwned
{
    public Guid TenantId { get; set; }
    public string Currency { get; set; } = "";
    public DateOnly Date { get; set; }
    /// <summary>Units of base currency per 1 unit of <see cref="Currency"/>.</summary>
    public decimal Rate { get; set; }
}

/// <summary>Sales tax (GST / provincial services tax). Output tax on invoices, input tax on bills.</summary>
public class TaxRate : BaseEntity, ITenantOwned
{
    public Guid TenantId { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public decimal Rate { get; set; }
    public string? Authority { get; set; }
    public Guid? OutputAccountId { get; set; }
    public Guid? InputAccountId { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>Customer, vendor or both.</summary>
public class Contact : BaseEntity, ITenantOwned
{
    public Guid TenantId { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public bool IsCustomer { get; set; }
    public bool IsVendor { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? Country { get; set; }
    public string? Ntn { get; set; }
    public string? Strn { get; set; }
    public string? Cnic { get; set; }
    public string? Currency { get; set; }
    public int PaymentTermsDays { get; set; } = 30;
    public bool IsActive { get; set; } = true;
}

/// <summary>Per-organization document numbering (JV-2027-00001, INV-2027-00001…).</summary>
public class NumberSequence : ITenantOwned
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string Key { get; set; } = "";
    /// <summary>Last number issued.</summary>
    public int Next { get; set; }
}

public class JournalEntry : BaseEntity, IEntityScoped
{
    public Guid TenantId { get; set; }
    public Guid EntityId { get; set; }
    public BusinessEntity? Entity { get; set; }
    public string Number { get; set; } = "";
    public DateOnly Date { get; set; }
    public string? Reference { get; set; }
    public string Description { get; set; } = "";
    public JournalStatus Status { get; set; } = JournalStatus.Draft;
    public JournalSource Source { get; set; } = JournalSource.Manual;
    /// <summary>The invoice, bill, payment or payroll run this entry was generated from.</summary>
    public Guid? SourceId { get; set; }
    public string Currency { get; set; } = "PKR";
    public decimal ExchangeRate { get; set; } = 1;
    public Guid? ReversalOfId { get; set; }
    public Guid? ReversedById { get; set; }
    public Guid? PostedBy { get; set; }
    public DateTime? PostedAt { get; set; }
    public ICollection<JournalLine> Lines { get; set; } = new List<JournalLine>();
}

public class JournalLine
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid JournalEntryId { get; set; }
    public JournalEntry? JournalEntry { get; set; }
    public Guid AccountId { get; set; }
    public Account? Account { get; set; }
    /// <summary>Entity dimension (branch/department) used to slice reports; defaults to the entry's entity.</summary>
    public Guid EntityId { get; set; }
    public string? Description { get; set; }
    /// <summary>Amounts in the entry's currency.</summary>
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    /// <summary>Amounts in the organization's base currency.</summary>
    public decimal BaseDebit { get; set; }
    public decimal BaseCredit { get; set; }
    public Guid? ContactId { get; set; }
    public Guid? TaxRateId { get; set; }
    public int SortOrder { get; set; }
}

/// <summary>Sales invoice or purchase bill.</summary>
public class FinanceDocument : BaseEntity, IEntityScoped
{
    public Guid TenantId { get; set; }
    public Guid EntityId { get; set; }
    public BusinessEntity? Entity { get; set; }
    public DocumentKind Kind { get; set; }
    public string Number { get; set; } = "";
    public Guid ContactId { get; set; }
    public Contact? Contact { get; set; }
    public DateOnly Date { get; set; }
    public DateOnly DueDate { get; set; }
    /// <summary>Vendor's own bill number, customer PO, booking reference…</summary>
    public string? Reference { get; set; }
    public string? Notes { get; set; }
    public string Currency { get; set; } = "PKR";
    public decimal ExchangeRate { get; set; } = 1;
    public DocumentStatus Status { get; set; } = DocumentStatus.Draft;

    public decimal Subtotal { get; set; }
    public decimal TaxTotal { get; set; }
    public decimal Total { get; set; }
    public decimal AmountPaid { get; set; }
    /// <summary>Total in base currency as posted to receivables/payables, and how much of it payments have cleared.</summary>
    public decimal BaseTotal { get; set; }
    public decimal BasePaid { get; set; }

    public Guid? JournalEntryId { get; set; }
    /// <summary>Bills created from a purchase order are three-way matched against it.</summary>
    public Guid? PurchaseOrderId { get; set; }
    public ICollection<FinanceDocumentLine> Lines { get; set; } = new List<FinanceDocumentLine>();
}

public class FinanceDocumentLine
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DocumentId { get; set; }
    public string Description { get; set; } = "";
    /// <summary>Income account on invoices, expense/asset account on bills.</summary>
    public Guid AccountId { get; set; }
    public Account? Account { get; set; }
    public decimal Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; }
    public decimal Amount { get; set; }
    public Guid? TaxRateId { get; set; }
    public TaxRate? TaxRate { get; set; }
    public decimal TaxAmount { get; set; }
    public Guid? PurchaseOrderLineId { get; set; }
    /// <summary>Goods-received-not-invoiced value this line cleared at approval (reverted if the bill is voided).</summary>
    public decimal MatchedBaseValue { get; set; }
    public int SortOrder { get; set; }
}

/// <summary>Money received from a customer or paid to a vendor, allocated to invoices/bills.</summary>
public class Payment : BaseEntity, IEntityScoped
{
    public Guid TenantId { get; set; }
    public Guid EntityId { get; set; }
    public BusinessEntity? Entity { get; set; }
    public PaymentKind Kind { get; set; }
    public string Number { get; set; } = "";
    public Guid ContactId { get; set; }
    public Contact? Contact { get; set; }
    public DateOnly Date { get; set; }
    public Guid BankAccountId { get; set; }
    public Account? BankAccount { get; set; }
    public string Currency { get; set; } = "PKR";
    public decimal ExchangeRate { get; set; } = 1;
    public decimal Amount { get; set; }
    public string? Reference { get; set; }
    public string? Notes { get; set; }
    public bool IsVoid { get; set; }
    public Guid? JournalEntryId { get; set; }
    public ICollection<PaymentAllocation> Allocations { get; set; } = new List<PaymentAllocation>();
}

public class PaymentAllocation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PaymentId { get; set; }
    public Guid DocumentId { get; set; }
    public FinanceDocument? Document { get; set; }
    public decimal Amount { get; set; }
    /// <summary>Receivable/payable cleared in base currency (at the document's rate).</summary>
    public decimal BaseAmount { get; set; }
}
