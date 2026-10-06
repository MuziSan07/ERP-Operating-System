using Erpos.Domain.Common;
using Erpos.Domain.Enums;

namespace Erpos.Domain.Entities;

/// <summary>A salary head such as Basic, House Rent or Loan Recovery.</summary>
public class PayComponent : BaseEntity, ITenantOwned
{
    public Guid TenantId { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public PayComponentKind Kind { get; set; }
    public bool IsTaxable { get; set; } = true;
    /// <summary>Marks the Basic Salary head (drives PF and exemption caps).</summary>
    public bool IsBasic { get; set; }
    /// <summary>Tax-exempt up to this fraction of basic (e.g. 0.10 for medical allowance).</summary>
    public decimal? ExemptUpToFractionOfBasic { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>An employee's monthly salary from <see cref="EffectiveFrom"/> until the next revision.</summary>
public class EmployeeSalary : BaseEntity, IEntityScoped
{
    public Guid TenantId { get; set; }
    public Guid EntityId { get; set; }
    public Guid EmployeeId { get; set; }
    public Employee? Employee { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public string? Remarks { get; set; }
    public ICollection<EmployeeSalaryLine> Lines { get; set; } = new List<EmployeeSalaryLine>();
}

public class EmployeeSalaryLine
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EmployeeSalaryId { get; set; }
    public Guid PayComponentId { get; set; }
    public PayComponent? PayComponent { get; set; }
    public decimal Amount { get; set; }
}

/// <summary>FBR salaried-individual tax table for one tax year (July–June). <see cref="Year"/> is the ending year.</summary>
public class TaxYear : BaseEntity, ITenantOwned
{
    public Guid TenantId { get; set; }
    public int Year { get; set; }
    public decimal? SurchargeThreshold { get; set; }
    public decimal SurchargeRate { get; set; }
    public string? Notes { get; set; }
    public ICollection<TaxSlab> Slabs { get; set; } = new List<TaxSlab>();
}

/// <summary>Tax = FixedTax + Rate × (income − From) for income above From (and up to To).</summary>
public class TaxSlab
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TaxYearId { get; set; }
    public decimal From { get; set; }
    public decimal? To { get; set; }
    public decimal FixedTax { get; set; }
    public decimal Rate { get; set; }
}

public class PayrollRun : BaseEntity, IEntityScoped
{
    public Guid TenantId { get; set; }
    public Guid EntityId { get; set; }
    public BusinessEntity? Entity { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public bool IncludeSubEntities { get; set; } = true;
    public PayrollRunStatus Status { get; set; } = PayrollRunStatus.Draft;
    public string? Notes { get; set; }
    /// <summary>Issues found during the last calculation, one per line (skipped employees etc.).</summary>
    public string? Warnings { get; set; }

    public decimal TotalGross { get; set; }
    public decimal TotalDeductions { get; set; }
    public decimal TotalNet { get; set; }
    public decimal TotalEmployerContributions { get; set; }

    public Guid? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public Guid? PostedBy { get; set; }
    public DateTime? PostedAt { get; set; }
    /// <summary>Accrual journal created when the run is posted (finance module enabled).</summary>
    public Guid? JournalEntryId { get; set; }
    /// <summary>Journal recording the bank transfer of net salaries.</summary>
    public Guid? PaymentJournalEntryId { get; set; }

    public ICollection<Payslip> Payslips { get; set; } = new List<Payslip>();
    public ICollection<PayrollAdjustment> Adjustments { get; set; } = new List<PayrollAdjustment>();
}

/// <summary>One-off line for one employee in one run (bonus, arrears, advance recovery). Survives recalculation.</summary>
public class PayrollAdjustment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PayrollRunId { get; set; }
    public Guid EmployeeId { get; set; }
    public string Name { get; set; } = "";
    public PayComponentKind Kind { get; set; }
    public decimal Amount { get; set; }
    public bool IsTaxable { get; set; } = true;
}

/// <summary>Calculated pay for one employee in one run. Employee details are snapshotted.</summary>
public class Payslip : BaseEntity, IEntityScoped
{
    public Guid TenantId { get; set; }
    public Guid EntityId { get; set; }
    public Guid PayrollRunId { get; set; }
    public PayrollRun? PayrollRun { get; set; }
    public Guid EmployeeId { get; set; }
    public Employee? Employee { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }

    public string EmployeeCode { get; set; } = "";
    public string EmployeeName { get; set; } = "";
    public string? Designation { get; set; }
    public string? Department { get; set; }
    public string? EntityName { get; set; }
    public string? Cnic { get; set; }
    public string? Iban { get; set; }
    public string? BankName { get; set; }

    public int DaysInMonth { get; set; }
    public decimal UnpaidDays { get; set; }
    public decimal PayableDays { get; set; }

    /// <summary>Full monthly salary before unpaid-day deductions.</summary>
    public decimal MonthlyGross { get; set; }
    public decimal GrossEarnings { get; set; }
    public decimal TaxableIncome { get; set; }
    public decimal IncomeTax { get; set; }
    public decimal TotalDeductions { get; set; }
    public decimal NetPay { get; set; }
    public decimal EmployerContributions { get; set; }
    /// <summary>Projected annual taxable income and tax used for this month's withholding (for the payslip footnote).</summary>
    public decimal ProjectedAnnualTaxable { get; set; }
    public decimal ProjectedAnnualTax { get; set; }

    public ICollection<PayslipLine> Lines { get; set; } = new List<PayslipLine>();
}

public class PayslipLine
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PayslipId { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public PayComponentKind Kind { get; set; }
    public decimal Amount { get; set; }
    /// <summary>True for employer contributions shown for information (not deducted from pay).</summary>
    public bool IsEmployerContribution { get; set; }
    public int SortOrder { get; set; }
}
