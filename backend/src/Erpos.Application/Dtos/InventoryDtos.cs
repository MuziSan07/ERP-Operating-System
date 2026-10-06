using Erpos.Domain.Enums;

namespace Erpos.Application.Dtos;

// ---- Master data ----
public record ItemCategoryDto(Guid Id, string Code, string Name);
public record SaveItemCategoryRequest(string Code, string Name);
public record ItemDto(Guid Id, string Code, string Name, Guid? CategoryId, string? CategoryName, ItemType Type, string Unit, string? Barcode,
    bool TrackBatches, bool TrackExpiry, decimal ReorderLevel, decimal? StandardCost, Guid? InventoryAccountId, Guid? ConsumptionAccountId,
    Guid? PurchaseTaxRateId, bool IsActive, decimal OnHand, decimal Value);
public record SaveItemRequest(string Code, string Name, Guid? CategoryId, ItemType Type, string Unit, string? Barcode, bool TrackBatches,
    bool TrackExpiry, decimal ReorderLevel, decimal? StandardCost, Guid? InventoryAccountId, Guid? ConsumptionAccountId, Guid? PurchaseTaxRateId,
    bool IsActive);
public record WarehouseDto(Guid Id, Guid EntityId, string EntityName, string Code, string Name, string? Address, bool IsActive, decimal StockValue);
public record SaveWarehouseRequest(Guid EntityId, string Code, string Name, string? Address, bool IsActive);

// ---- Stock ----
public record StockRow(Guid ItemId, string ItemCode, string ItemName, string Unit, string? Category, Guid WarehouseId, string WarehouseName,
    decimal Quantity, decimal AverageCost, decimal Value, decimal ReorderLevel, bool BelowReorder);
public record BatchStockRow(Guid ItemId, string ItemCode, string ItemName, Guid WarehouseId, string WarehouseName, Guid BatchId, string BatchNo,
    DateOnly? ExpiryDate, int? DaysToExpiry, decimal Quantity);
public record MovementRow(DateOnly Date, StockMovementType Type, string ItemCode, string ItemName, string WarehouseName, string? BatchNo,
    decimal Quantity, decimal UnitCost, decimal Value, decimal QuantityAfter, decimal AverageCostAfter, string? Reference, Guid SourceId);
public record StockValuationDto(string Currency, decimal StockValue, decimal LedgerValue, decimal Difference, IReadOnlyList<StockRow> Rows);

public record StockLineInput(Guid ItemId, decimal Quantity, Guid? BatchId, string? BatchNo, DateOnly? ExpiryDate, decimal? UnitCost, string? Notes);
public record CreateStockTransactionRequest(StockTransactionType Type, Guid WarehouseId, Guid? ToWarehouseId, DateOnly Date, Guid? AccountId,
    Guid? ChargeEntityId, string? Reference, string? Notes, List<StockLineInput> Lines);
public record StockTransactionLineDto(Guid ItemId, string ItemCode, string ItemName, string Unit, string? BatchNo, DateOnly? ExpiryDate,
    decimal Quantity, decimal UnitCost, decimal Value, string? Notes);
public record StockTransactionDto(Guid Id, StockTransactionType Type, string Number, DateOnly Date, Guid WarehouseId, string WarehouseName,
    Guid? ToWarehouseId, string? ToWarehouseName, string? AccountName, string? Reference, string? Notes, decimal TotalValue, Guid? JournalEntryId,
    string? CreatedByName, IReadOnlyList<StockTransactionLineDto> Lines);

// ---- Procurement ----
public record PrLineInput(Guid ItemId, decimal Quantity, decimal EstimatedUnitPrice, string? Notes);
public record SavePurchaseRequestRequest(Guid EntityId, DateOnly Date, DateOnly? RequiredBy, string Purpose, List<PrLineInput> Lines);
public record PrLineDto(Guid Id, Guid ItemId, string ItemCode, string ItemName, string Unit, decimal Quantity, decimal EstimatedUnitPrice,
    decimal QuantityOrdered, string? Notes);
public record PurchaseRequestDto(Guid Id, string Number, Guid EntityId, string EntityName, DateOnly Date, DateOnly? RequiredBy, string Purpose,
    PurchaseRequestStatus Status, string? RequestedByName, string? ApprovedByName, DateTime? ApprovedAt, string? DecisionComment,
    decimal EstimatedTotal, IReadOnlyList<PrLineDto> Lines);
public record DecisionRequest(bool Approve, string? Comment);

public record PoLineInput(Guid ItemId, string? Description, decimal Quantity, decimal UnitPrice, Guid? TaxRateId, Guid? PurchaseRequestLineId);
public record SavePurchaseOrderRequest(Guid EntityId, Guid VendorId, Guid WarehouseId, DateOnly Date, DateOnly? ExpectedDate, string? Currency,
    decimal? ExchangeRate, string? Terms, string? Notes, List<PoLineInput> Lines);
public record PoLineDto(Guid Id, Guid ItemId, string ItemCode, string ItemName, string Unit, ItemType ItemType, bool TrackBatches, bool TrackExpiry,
    string Description, decimal Quantity, decimal UnitPrice, Guid? TaxRateId, string? TaxRateName, decimal Amount, decimal TaxAmount,
    decimal QuantityReceived, decimal QuantityBilled, Guid? PurchaseRequestLineId);
public record PurchaseOrderDto(Guid Id, string Number, Guid EntityId, string EntityName, Guid VendorId, string VendorName, string? VendorNtn,
    string? VendorStrn, string? VendorAddress, DateOnly Date, DateOnly? ExpectedDate, Guid WarehouseId, string WarehouseName, string Currency,
    decimal ExchangeRate, string? Terms, string? Notes, PurchaseOrderStatus Status, decimal Subtotal, decimal TaxTotal, decimal Total,
    string? CreatedByName, string? ApprovedByName, DateTime? ApprovedAt, IReadOnlyList<PoLineDto> Lines,
    IReadOnlyList<LinkedDocDto> Receipts, IReadOnlyList<LinkedDocDto> Bills);
public record LinkedDocDto(Guid Id, string? Number, DateOnly Date, decimal Value, string Status);
public record PurchaseOrderListItem(Guid Id, string Number, string EntityName, string VendorName, DateOnly Date, DateOnly? ExpectedDate,
    string WarehouseName, string Currency, PurchaseOrderStatus Status, decimal Total, decimal ReceivedPercent, decimal BilledPercent);

public record GrnLineInput(Guid PurchaseOrderLineId, decimal Quantity, string? BatchNo, DateOnly? ExpiryDate);
public record CreateGoodsReceiptRequest(Guid PurchaseOrderId, DateOnly Date, string? DeliveryNote, string? Notes, List<GrnLineInput> Lines);
public record GrnLineDto(Guid ItemId, string ItemCode, string ItemName, string Unit, decimal Quantity, string? BatchNo, DateOnly? ExpiryDate,
    decimal UnitCost, decimal Value);
public record GoodsReceiptDto(Guid Id, string Number, Guid PurchaseOrderId, string PurchaseOrderNumber, string VendorName, Guid WarehouseId,
    string WarehouseName, DateOnly Date, string? DeliveryNote, string? Notes, decimal TotalValue, Guid? JournalEntryId, string? CreatedByName,
    IReadOnlyList<GrnLineDto> Lines);
public record GrniRow(Guid PurchaseOrderId, string PurchaseOrderNumber, string VendorName, string ItemCode, string ItemName,
    decimal QuantityReceived, decimal QuantityBilled, decimal UnbilledValue);
