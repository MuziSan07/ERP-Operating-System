using Erpos.Domain.Common;
using Erpos.Domain.Enums;

namespace Erpos.Domain.Entities;

public class ItemCategory : BaseEntity, ITenantOwned
{
    public Guid TenantId { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
}

/// <summary>Something bought, stocked or consumed. Service items pass through procurement without stock.</summary>
public class Item : BaseEntity, ITenantOwned
{
    public Guid TenantId { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public Guid? CategoryId { get; set; }
    public ItemCategory? Category { get; set; }
    public ItemType Type { get; set; } = ItemType.Stock;
    public string Unit { get; set; } = "pcs";
    public string? Barcode { get; set; }
    public bool TrackBatches { get; set; }
    public bool TrackExpiry { get; set; }
    public decimal ReorderLevel { get; set; }
    public decimal? StandardCost { get; set; }
    /// <summary>Asset account for stock (default Inventories); for services, the expense account accrued on receipt.</summary>
    public Guid? InventoryAccountId { get; set; }
    /// <summary>Expense charged when stock is issued/consumed (default Cost of goods and services sold).</summary>
    public Guid? ConsumptionAccountId { get; set; }
    public Guid? PurchaseTaxRateId { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>A stock location belonging to a branch (entity).</summary>
public class Warehouse : BaseEntity, IEntityScoped
{
    public Guid TenantId { get; set; }
    public Guid EntityId { get; set; }
    public BusinessEntity? Entity { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Address { get; set; }
    public bool IsActive { get; set; } = true;
}

public class StockBatch : ITenantOwned
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid ItemId { get; set; }
    public string BatchNo { get; set; } = "";
    public DateOnly? ExpiryDate { get; set; }
    public DateOnly? ManufactureDate { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Quantity and value of an item in a warehouse. Average cost = Value / Quantity.</summary>
public class StockLevel : ITenantOwned
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid ItemId { get; set; }
    public Item? Item { get; set; }
    public Guid WarehouseId { get; set; }
    public Warehouse? Warehouse { get; set; }
    public decimal Quantity { get; set; }
    public decimal Value { get; set; }
    /// <summary>Optimistic concurrency: two people moving the same stock at once can't corrupt the average.</summary>
    public int Version { get; set; }
}

/// <summary>Quantity of one batch in one warehouse (value lives on <see cref="StockLevel"/>).</summary>
public class StockBatchLevel : ITenantOwned
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid ItemId { get; set; }
    public Guid WarehouseId { get; set; }
    public Guid BatchId { get; set; }
    public StockBatch? Batch { get; set; }
    public decimal Quantity { get; set; }
}

/// <summary>Immutable stock ledger. Quantity is positive for stock in, negative for stock out.</summary>
public class StockMovement : ITenantOwned
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid EntityId { get; set; }
    public DateOnly Date { get; set; }
    public Guid ItemId { get; set; }
    public Item? Item { get; set; }
    public Guid WarehouseId { get; set; }
    public Warehouse? Warehouse { get; set; }
    public Guid? BatchId { get; set; }
    public StockBatch? Batch { get; set; }
    public StockMovementType Type { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal Value { get; set; }
    public decimal QuantityAfter { get; set; }
    public decimal AverageCostAfter { get; set; }
    public string? Reference { get; set; }
    /// <summary>Goods receipt or stock transaction that caused the movement.</summary>
    public Guid SourceId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? CreatedBy { get; set; }
}

/// <summary>Issue (consumption), transfer, adjustment / stock count, or opening stock.</summary>
public class StockTransaction : BaseEntity, IEntityScoped
{
    public Guid TenantId { get; set; }
    public Guid EntityId { get; set; }
    public StockTransactionType Type { get; set; }
    public string Number { get; set; } = "";
    public DateOnly Date { get; set; }
    public Guid WarehouseId { get; set; }
    public Warehouse? Warehouse { get; set; }
    public Guid? ToWarehouseId { get; set; }
    public Warehouse? ToWarehouse { get; set; }
    /// <summary>Expense (issues) or offset account (adjustments / opening stock).</summary>
    public Guid? AccountId { get; set; }
    /// <summary>Department/branch charged for issued stock (defaults to the warehouse's entity).</summary>
    public Guid? ChargeEntityId { get; set; }
    public string? Reference { get; set; }
    public string? Notes { get; set; }
    public decimal TotalValue { get; set; }
    public Guid? JournalEntryId { get; set; }
    public ICollection<StockTransactionLine> Lines { get; set; } = new List<StockTransactionLine>();
}

public class StockTransactionLine
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StockTransactionId { get; set; }
    public Guid ItemId { get; set; }
    public Item? Item { get; set; }
    public Guid? BatchId { get; set; }
    public StockBatch? Batch { get; set; }
    /// <summary>Signed for adjustments; positive for issues/transfers/opening.</summary>
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal Value { get; set; }
    public string? Notes { get; set; }
}

// ---------------- Procurement ----------------

public class PurchaseRequest : BaseEntity, IEntityScoped
{
    public Guid TenantId { get; set; }
    public Guid EntityId { get; set; }
    public BusinessEntity? Entity { get; set; }
    public string Number { get; set; } = "";
    public DateOnly Date { get; set; }
    public DateOnly? RequiredBy { get; set; }
    public string Purpose { get; set; } = "";
    public PurchaseRequestStatus Status { get; set; } = PurchaseRequestStatus.Draft;
    public Guid? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? DecisionComment { get; set; }
    public ICollection<PurchaseRequestLine> Lines { get; set; } = new List<PurchaseRequestLine>();
}

public class PurchaseRequestLine
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PurchaseRequestId { get; set; }
    public Guid ItemId { get; set; }
    public Item? Item { get; set; }
    public decimal Quantity { get; set; }
    public decimal EstimatedUnitPrice { get; set; }
    public decimal QuantityOrdered { get; set; }
    public string? Notes { get; set; }
}

public class PurchaseOrder : BaseEntity, IEntityScoped
{
    public Guid TenantId { get; set; }
    public Guid EntityId { get; set; }
    public BusinessEntity? Entity { get; set; }
    public string Number { get; set; } = "";
    public Guid VendorId { get; set; }
    public Contact? Vendor { get; set; }
    public DateOnly Date { get; set; }
    public DateOnly? ExpectedDate { get; set; }
    public Guid WarehouseId { get; set; }
    public Warehouse? Warehouse { get; set; }
    public string Currency { get; set; } = "PKR";
    public decimal ExchangeRate { get; set; } = 1;
    public string? Terms { get; set; }
    public string? Notes { get; set; }
    public PurchaseOrderStatus Status { get; set; } = PurchaseOrderStatus.Draft;
    public decimal Subtotal { get; set; }
    public decimal TaxTotal { get; set; }
    public decimal Total { get; set; }
    public Guid? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public ICollection<PurchaseOrderLine> Lines { get; set; } = new List<PurchaseOrderLine>();
}

public class PurchaseOrderLine
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PurchaseOrderId { get; set; }
    public Guid ItemId { get; set; }
    public Item? Item { get; set; }
    public string Description { get; set; } = "";
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public Guid? TaxRateId { get; set; }
    public TaxRate? TaxRate { get; set; }
    public decimal Amount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal QuantityReceived { get; set; }
    public decimal QuantityBilled { get; set; }
    /// <summary>Base-currency value accrued to "goods received not invoiced" so far, and how much bills have cleared.</summary>
    public decimal ReceivedBaseValue { get; set; }
    public decimal BilledBaseValue { get; set; }
    public Guid? PurchaseRequestLineId { get; set; }
    public int SortOrder { get; set; }
}

/// <summary>Goods received note (GRN): stock in at PO price, accrued to "goods received not invoiced".</summary>
public class GoodsReceipt : BaseEntity, IEntityScoped
{
    public Guid TenantId { get; set; }
    public Guid EntityId { get; set; }
    public string Number { get; set; } = "";
    public Guid PurchaseOrderId { get; set; }
    public PurchaseOrder? PurchaseOrder { get; set; }
    public Guid WarehouseId { get; set; }
    public Warehouse? Warehouse { get; set; }
    public DateOnly Date { get; set; }
    public string? DeliveryNote { get; set; }
    public string? Notes { get; set; }
    public decimal TotalValue { get; set; }
    public Guid? JournalEntryId { get; set; }
    public ICollection<GoodsReceiptLine> Lines { get; set; } = new List<GoodsReceiptLine>();
}

public class GoodsReceiptLine
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GoodsReceiptId { get; set; }
    public Guid PurchaseOrderLineId { get; set; }
    public Guid ItemId { get; set; }
    public Item? Item { get; set; }
    public decimal Quantity { get; set; }
    public Guid? BatchId { get; set; }
    public StockBatch? Batch { get; set; }
    public decimal UnitCost { get; set; }
    public decimal Value { get; set; }
}
