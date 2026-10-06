using Erpos.Domain.Common;
using Erpos.Domain.Enums;

namespace Erpos.Domain.Entities;

/// <summary>Donor profile on top of a finance contact (the contact carries name, phone, NTN/CNIC for the ledger).</summary>
public class Donor : BaseEntity, ITenantOwned
{
    public Guid TenantId { get; set; }
    public Guid ContactId { get; set; }
    public Contact? Contact { get; set; }
    public DonorType Type { get; set; } = DonorType.Individual;
    public string? Notes { get; set; }
}

public class Fund : BaseEntity, ITenantOwned
{
    public Guid TenantId { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public FundKind Kind { get; set; }
    public string? Purpose { get; set; }
    /// <summary>Set when the fund was opened for a grant.</summary>
    public Guid? GrantId { get; set; }
    public bool IsActive { get; set; } = true;
}

public class NgoProgram : BaseEntity, IEntityScoped
{
    public Guid TenantId { get; set; }
    public Guid EntityId { get; set; }
    public BusinessEntity? Entity { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Sector { get; set; }
    public string? Description { get; set; }
    public int TargetBeneficiaries { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>Donor agreement with a budget, tranches (instalments) and reporting deadlines.</summary>
public class Grant : BaseEntity, IEntityScoped
{
    public Guid TenantId { get; set; }
    public Guid EntityId { get; set; }
    public BusinessEntity? Entity { get; set; }
    public string Number { get; set; } = "";
    public string Title { get; set; } = "";
    public Guid DonorId { get; set; }
    public Donor? Donor { get; set; }
    public string? AgreementRef { get; set; }
    public Guid? ProgramId { get; set; }
    public NgoProgram? Program { get; set; }
    public Guid FundId { get; set; }
    public Fund? Fund { get; set; }
    /// <summary>Grant currency; budget and tranches are in it. Expenses are recorded in base currency.</summary>
    public string Currency { get; set; } = "PKR";
    /// <summary>Base currency per unit of grant currency at signing; used until money is received.</summary>
    public decimal AgreementRate { get; set; } = 1;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public GrantStatus Status { get; set; } = GrantStatus.Proposal;
    public ReportingFrequency ReportingFrequency { get; set; } = ReportingFrequency.Quarterly;
    /// <summary>Overspend allowed on a budget line without donor approval (percent of the line).</summary>
    public decimal FlexibilityPercent { get; set; } = 10;
    public string? Notes { get; set; }
    public ICollection<GrantBudgetLine> BudgetLines { get; set; } = new List<GrantBudgetLine>();
    public ICollection<GrantTranche> Tranches { get; set; } = new List<GrantTranche>();
    public ICollection<GrantReport> Reports { get; set; } = new List<GrantReport>();
}

public class GrantBudgetLine
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GrantId { get; set; }
    public string Code { get; set; } = "";
    public string Description { get; set; } = "";
    public BudgetCategory Category { get; set; }
    /// <summary>In grant currency.</summary>
    public decimal Amount { get; set; }
    public Guid? ExpenseAccountId { get; set; }
    public int SortOrder { get; set; }
}

public class GrantTranche
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GrantId { get; set; }
    public int Sequence { get; set; }
    public DateOnly DueDate { get; set; }
    /// <summary>In grant currency.</summary>
    public decimal Amount { get; set; }
    public string? Condition { get; set; }
    public DateOnly? ReceivedDate { get; set; }
    public decimal? ReceivedAmount { get; set; }
    public decimal? ReceivedBase { get; set; }
    public Guid? BankAccountId { get; set; }
    public Guid? JournalEntryId { get; set; }
}

public class GrantReport
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GrantId { get; set; }
    public string Title { get; set; } = "";
    public DateOnly PeriodStart { get; set; }
    public DateOnly PeriodEnd { get; set; }
    public DateOnly DueDate { get; set; }
    public DateOnly? SubmittedOn { get; set; }
    public string? Notes { get; set; }
}

/// <summary>A gift from a donor into a fund; the number doubles as the donation receipt number.</summary>
public class Donation : BaseEntity, IEntityScoped
{
    public Guid TenantId { get; set; }
    public Guid EntityId { get; set; }
    public string Number { get; set; } = "";
    public DateOnly Date { get; set; }
    public Guid DonorId { get; set; }
    public Donor? Donor { get; set; }
    public Guid FundId { get; set; }
    public Fund? Fund { get; set; }
    public decimal Amount { get; set; }
    public DonationMethod Method { get; set; }
    public string? Reference { get; set; }
    public Guid BankAccountId { get; set; }
    public Guid? ProgramId { get; set; }
    public string? Notes { get; set; }
    public Guid? JournalEntryId { get; set; }
}

/// <summary>
/// Spending charged to a fund (and, for grants, to a budget line). Paid from cash/bank, owed to a vendor (approved bill),
/// or an allocation of a cost already booked elsewhere (e.g. salaries). Restricted spending releases deferred income.
/// </summary>
public class FundExpense : BaseEntity, IEntityScoped
{
    public Guid TenantId { get; set; }
    public Guid EntityId { get; set; }
    public string Number { get; set; } = "";
    public DateOnly Date { get; set; }
    public Guid FundId { get; set; }
    public Fund? Fund { get; set; }
    public Guid? GrantId { get; set; }
    public Guid? BudgetLineId { get; set; }
    public Guid? ProgramId { get; set; }
    public NgoProgram? Program { get; set; }
    public FunctionalCategory Function { get; set; } = FunctionalCategory.Program;
    public Guid AccountId { get; set; }
    public string Description { get; set; } = "";
    /// <summary>Base currency.</summary>
    public decimal Amount { get; set; }
    public Guid? PaidFromAccountId { get; set; }
    public Guid? VendorId { get; set; }
    public bool AllocationOnly { get; set; }
    public Guid? BillId { get; set; }
    public Guid? JournalEntryId { get; set; }
    public Guid? ReleaseJournalId { get; set; }
    public Guid? AssistanceId { get; set; }
}

public class Beneficiary : BaseEntity, IEntityScoped
{
    public Guid TenantId { get; set; }
    public Guid EntityId { get; set; }
    public string RegistrationNo { get; set; } = "";
    public string FullName { get; set; } = "";
    public string? Cnic { get; set; }
    public Gender Gender { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public string? Phone { get; set; }
    public string? District { get; set; }
    public string? Address { get; set; }
    public int HouseholdSize { get; set; } = 1;
    public string? Vulnerabilities { get; set; }
    /// <summary>Verified as eligible to receive Zakat.</summary>
    public bool ZakatEligible { get; set; }
    public Guid? ProgramId { get; set; }
    public NgoProgram? Program { get; set; }
    public DateOnly EnrolledOn { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>Help given to a beneficiary. Cash assistance is charged to a fund through a <see cref="FundExpense"/>.</summary>
public class Assistance : BaseEntity, ITenantOwned
{
    public Guid TenantId { get; set; }
    public Guid BeneficiaryId { get; set; }
    public Beneficiary? Beneficiary { get; set; }
    public Guid? ProgramId { get; set; }
    public DateOnly Date { get; set; }
    public AssistanceType Type { get; set; }
    public string Description { get; set; } = "";
    public decimal? Quantity { get; set; }
    public decimal Value { get; set; }
    public Guid? FundId { get; set; }
    public Guid? ExpenseId { get; set; }
}
