using Erpos.Domain.Common;
using Erpos.Domain.Enums;

namespace Erpos.Domain.Entities;

/// <summary>Defaults for a class of assets (method, life, accounts); each asset can override method and life.</summary>
public class AssetCategory : BaseEntity, ITenantOwned
{
    public Guid TenantId { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public DepreciationMethod Method { get; set; } = DepreciationMethod.StraightLine;
    public int UsefulLifeMonths { get; set; }
    /// <summary>Annual rate for reducing balance, e.g. 0.15.</summary>
    public decimal ReducingRate { get; set; }
    public Guid AssetAccountId { get; set; }
    public Guid AccumulatedAccountId { get; set; }
    public Guid ExpenseAccountId { get; set; }
    public bool IsActive { get; set; } = true;
}

public class FixedAsset : BaseEntity, IEntityScoped
{
    public Guid TenantId { get; set; }
    public Guid EntityId { get; set; }
    public BusinessEntity? Entity { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public Guid CategoryId { get; set; }
    public AssetCategory? Category { get; set; }
    public string? SerialNo { get; set; }
    public string? Location { get; set; }
    public Guid? CustodianEmployeeId { get; set; }
    public DateOnly AcquisitionDate { get; set; }
    /// <summary>Depreciation starts in this month (full-month convention).</summary>
    public DateOnly DepreciationStart { get; set; }
    public decimal Cost { get; set; }
    public decimal SalvageValue { get; set; }
    public DepreciationMethod Method { get; set; }
    public int UsefulLifeMonths { get; set; }
    public decimal ReducingRate { get; set; }
    public decimal AccumulatedDepreciation { get; set; }
    /// <summary>Last month depreciated, as yyyymm (0: never).</summary>
    public int DepreciatedThrough { get; set; }
    public AssetStatus Status { get; set; } = AssetStatus.Active;
    public DateOnly? DisposalDate { get; set; }
    public decimal? DisposalProceeds { get; set; }
    public Guid? AcquisitionJournalId { get; set; }
    public Guid? AcquisitionBillId { get; set; }
    public Guid? DisposalJournalId { get; set; }
    public string? Notes { get; set; }
}

/// <summary>One month's depreciation posted for one entity.</summary>
public class DepreciationRun : BaseEntity, IEntityScoped
{
    public Guid TenantId { get; set; }
    public Guid EntityId { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public decimal Total { get; set; }
    public Guid? JournalEntryId { get; set; }
    public ICollection<DepreciationLine> Lines { get; set; } = new List<DepreciationLine>();
}

public class DepreciationLine
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RunId { get; set; }
    public Guid AssetId { get; set; }
    public int Months { get; set; }
    public decimal Amount { get; set; }
}
