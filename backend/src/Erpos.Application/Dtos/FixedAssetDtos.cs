using Erpos.Domain.Enums;

namespace Erpos.Application.Dtos;

public record AssetCategoryDto(Guid Id, string Code, string Name, DepreciationMethod Method, int UsefulLifeMonths, decimal ReducingRate, Guid AssetAccountId,
    Guid AccumulatedAccountId, Guid ExpenseAccountId, bool IsActive, int Assets);
public record SaveAssetCategoryRequest(string Code, string Name, DepreciationMethod Method, int UsefulLifeMonths, decimal ReducingRate, Guid? AssetAccountId,
    Guid? AccumulatedAccountId, Guid? ExpenseAccountId, bool IsActive);

public record RegisterAssetRequest(Guid EntityId, string Name, Guid CategoryId, string? SerialNo, string? Location, DateOnly AcquisitionDate,
    DateOnly? DepreciationStart, decimal Cost, decimal SalvageValue, DepreciationMethod? Method, int? UsefulLifeMonths, decimal? ReducingRate,
    AssetAcquisition Acquisition, Guid? PaidFromAccountId, Guid? VendorId, decimal OpeningAccumulatedDepreciation, DateOnly? DepreciatedThrough, string? Notes);
public record UpdateAssetRequest(string Name, string? SerialNo, string? Location, string? Notes);
public record AssetListItem(Guid Id, string Code, string Name, string CategoryName, string EntityName, DateOnly AcquisitionDate, decimal Cost,
    decimal AccumulatedDepreciation, decimal BookValue, AssetStatus Status, string? Location, string? DepreciatedThrough);
public record AssetDepreciationRow(int Year, int Month, int Months, decimal Amount);
public record AssetDto(Guid Id, string Code, string Name, Guid EntityId, string EntityName, Guid CategoryId, string CategoryName, string? SerialNo,
    string? Location, DateOnly AcquisitionDate, DateOnly DepreciationStart, decimal Cost, decimal SalvageValue, DepreciationMethod Method,
    int UsefulLifeMonths, decimal ReducingRate, decimal AccumulatedDepreciation, decimal BookValue, decimal MonthlyCharge, string? DepreciatedThrough,
    AssetStatus Status, DateOnly? DisposalDate, decimal? DisposalProceeds, string? Notes, IReadOnlyList<AssetDepreciationRow> History);

public record RunDepreciationRequest(Guid EntityId, int Year, int Month, DateOnly? PostingDate);
public record DepreciationRunDto(Guid Id, int Year, int Month, decimal Total, int Assets, string? JournalNumber, IReadOnlyList<DepreciationRunLine> Lines);
public record DepreciationRunLine(Guid AssetId, string AssetCode, string AssetName, int Months, decimal Amount);
public record DisposeAssetRequest(DateOnly Date, decimal Proceeds, Guid? ReceivedIntoAccountId, string? Reason);

public record AssetScheduleRow(string Category, decimal OpeningCost, decimal Additions, decimal Disposals, decimal ClosingCost, decimal OpeningDepreciation,
    decimal Charge, decimal DepreciationOnDisposals, decimal ClosingDepreciation, decimal ClosingBookValue);
public record AssetScheduleDto(DateOnly From, DateOnly To, IReadOnlyList<AssetScheduleRow> Rows, AssetScheduleRow Total);
