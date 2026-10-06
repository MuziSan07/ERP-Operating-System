using Erpos.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Erpos.Infrastructure.Persistence;

public class ItemCategoryConfig : IEntityTypeConfiguration<ItemCategory>
{
    public void Configure(EntityTypeBuilder<ItemCategory> b)
    {
        b.ToTable("inv_categories");
        b.Property(x => x.Code).HasMaxLength(50).IsRequired();
        b.Property(x => x.Name).HasMaxLength(100).IsRequired();
        b.HasIndex(x => new { x.TenantId, x.Code });
    }
}

public class ItemConfig : IEntityTypeConfiguration<Item>
{
    public void Configure(EntityTypeBuilder<Item> b)
    {
        b.ToTable("inv_items");
        b.Property(x => x.Code).HasMaxLength(50).IsRequired();
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.Unit).HasMaxLength(20).IsRequired();
        b.Property(x => x.Barcode).HasMaxLength(64);
        b.Property(x => x.ReorderLevel).HasPrecision(18, 4);
        b.Property(x => x.StandardCost).HasPrecision(18, 4);
        b.HasOne(x => x.Category).WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.SetNull);
        b.HasIndex(x => new { x.TenantId, x.Code });
        b.HasIndex(x => new { x.TenantId, x.Barcode });
    }
}

public class WarehouseConfig : IEntityTypeConfiguration<Warehouse>
{
    public void Configure(EntityTypeBuilder<Warehouse> b)
    {
        b.ToTable("inv_warehouses");
        b.Property(x => x.Code).HasMaxLength(50).IsRequired();
        b.Property(x => x.Name).HasMaxLength(150).IsRequired();
        b.Property(x => x.Address).HasMaxLength(500);
        b.HasOne(x => x.Entity).WithMany().HasForeignKey(x => x.EntityId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.TenantId, x.Code });
    }
}

public class StockBatchConfig : IEntityTypeConfiguration<StockBatch>
{
    public void Configure(EntityTypeBuilder<StockBatch> b)
    {
        b.ToTable("inv_batches");
        b.Property(x => x.BatchNo).HasMaxLength(64).IsRequired();
        b.HasIndex(x => new { x.ItemId, x.BatchNo }).IsUnique();
    }
}

public class StockLevelConfig : IEntityTypeConfiguration<StockLevel>
{
    public void Configure(EntityTypeBuilder<StockLevel> b)
    {
        b.ToTable("inv_stock_levels");
        b.Property(x => x.Quantity).HasPrecision(18, 4);
        b.Property(x => x.Value).HasPrecision(18, 4);
        b.Property(x => x.Version).IsConcurrencyToken();
        b.HasOne(x => x.Item).WithMany().HasForeignKey(x => x.ItemId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Warehouse).WithMany().HasForeignKey(x => x.WarehouseId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.ItemId, x.WarehouseId }).IsUnique();
    }
}

public class StockBatchLevelConfig : IEntityTypeConfiguration<StockBatchLevel>
{
    public void Configure(EntityTypeBuilder<StockBatchLevel> b)
    {
        b.ToTable("inv_batch_levels");
        b.Property(x => x.Quantity).HasPrecision(18, 4);
        b.HasOne(x => x.Batch).WithMany().HasForeignKey(x => x.BatchId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.ItemId, x.WarehouseId, x.BatchId }).IsUnique();
    }
}

public class StockMovementConfig : IEntityTypeConfiguration<StockMovement>
{
    public void Configure(EntityTypeBuilder<StockMovement> b)
    {
        b.ToTable("inv_movements");
        b.Property(x => x.Quantity).HasPrecision(18, 4);
        b.Property(x => x.UnitCost).HasPrecision(18, 4);
        b.Property(x => x.QuantityAfter).HasPrecision(18, 4);
        b.Property(x => x.AverageCostAfter).HasPrecision(18, 4);
        b.Property(x => x.Reference).HasMaxLength(50);
        b.HasOne(x => x.Item).WithMany().HasForeignKey(x => x.ItemId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Warehouse).WithMany().HasForeignKey(x => x.WarehouseId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Batch).WithMany().HasForeignKey(x => x.BatchId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.TenantId, x.ItemId, x.Date });
        b.HasIndex(x => x.SourceId);
    }
}

public class StockTransactionConfig : IEntityTypeConfiguration<StockTransaction>
{
    public void Configure(EntityTypeBuilder<StockTransaction> b)
    {
        b.ToTable("inv_transactions");
        b.Property(x => x.Number).HasMaxLength(30);
        b.Property(x => x.Reference).HasMaxLength(100);
        b.Property(x => x.Notes).HasMaxLength(1000);
        b.HasOne(x => x.Warehouse).WithMany().HasForeignKey(x => x.WarehouseId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.ToWarehouse).WithMany().HasForeignKey(x => x.ToWarehouseId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Lines).WithOne().HasForeignKey(l => l.StockTransactionId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.TenantId, x.Date });
    }
}

public class StockTransactionLineConfig : IEntityTypeConfiguration<StockTransactionLine>
{
    public void Configure(EntityTypeBuilder<StockTransactionLine> b)
    {
        b.ToTable("inv_transaction_lines");
        b.Property(x => x.Quantity).HasPrecision(18, 4);
        b.Property(x => x.UnitCost).HasPrecision(18, 4);
        b.Property(x => x.Notes).HasMaxLength(300);
        b.HasOne(x => x.Item).WithMany().HasForeignKey(x => x.ItemId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Batch).WithMany().HasForeignKey(x => x.BatchId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class PurchaseRequestConfig : IEntityTypeConfiguration<PurchaseRequest>
{
    public void Configure(EntityTypeBuilder<PurchaseRequest> b)
    {
        b.ToTable("proc_requests");
        b.Property(x => x.Number).HasMaxLength(30);
        b.Property(x => x.Purpose).HasMaxLength(500).IsRequired();
        b.Property(x => x.DecisionComment).HasMaxLength(500);
        b.HasOne(x => x.Entity).WithMany().HasForeignKey(x => x.EntityId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Lines).WithOne().HasForeignKey(l => l.PurchaseRequestId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.TenantId, x.Status });
    }
}

public class PurchaseRequestLineConfig : IEntityTypeConfiguration<PurchaseRequestLine>
{
    public void Configure(EntityTypeBuilder<PurchaseRequestLine> b)
    {
        b.ToTable("proc_request_lines");
        b.Property(x => x.Quantity).HasPrecision(18, 4);
        b.Property(x => x.QuantityOrdered).HasPrecision(18, 4);
        b.Property(x => x.EstimatedUnitPrice).HasPrecision(18, 4);
        b.Property(x => x.Notes).HasMaxLength(300);
        b.HasOne(x => x.Item).WithMany().HasForeignKey(x => x.ItemId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class PurchaseOrderConfig : IEntityTypeConfiguration<PurchaseOrder>
{
    public void Configure(EntityTypeBuilder<PurchaseOrder> b)
    {
        b.ToTable("proc_orders");
        b.Property(x => x.Number).HasMaxLength(30);
        b.Property(x => x.Currency).HasMaxLength(3);
        b.Property(x => x.ExchangeRate).HasPrecision(18, 6);
        b.Property(x => x.Terms).HasMaxLength(1000);
        b.Property(x => x.Notes).HasMaxLength(1000);
        b.HasOne(x => x.Entity).WithMany().HasForeignKey(x => x.EntityId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Vendor).WithMany().HasForeignKey(x => x.VendorId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Warehouse).WithMany().HasForeignKey(x => x.WarehouseId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Lines).WithOne().HasForeignKey(l => l.PurchaseOrderId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.TenantId, x.Status });
    }
}

public class PurchaseOrderLineConfig : IEntityTypeConfiguration<PurchaseOrderLine>
{
    public void Configure(EntityTypeBuilder<PurchaseOrderLine> b)
    {
        b.ToTable("proc_order_lines");
        b.Property(x => x.Description).HasMaxLength(300).IsRequired();
        foreach (var p in new[] { nameof(PurchaseOrderLine.Quantity), nameof(PurchaseOrderLine.UnitPrice), nameof(PurchaseOrderLine.QuantityReceived), nameof(PurchaseOrderLine.QuantityBilled) })
            b.Property(p).HasPrecision(18, 4);
        b.HasOne(x => x.Item).WithMany().HasForeignKey(x => x.ItemId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.TaxRate).WithMany().HasForeignKey(x => x.TaxRateId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class GoodsReceiptConfig : IEntityTypeConfiguration<GoodsReceipt>
{
    public void Configure(EntityTypeBuilder<GoodsReceipt> b)
    {
        b.ToTable("proc_goods_receipts");
        b.Property(x => x.Number).HasMaxLength(30);
        b.Property(x => x.DeliveryNote).HasMaxLength(100);
        b.Property(x => x.Notes).HasMaxLength(1000);
        b.HasOne(x => x.PurchaseOrder).WithMany().HasForeignKey(x => x.PurchaseOrderId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Warehouse).WithMany().HasForeignKey(x => x.WarehouseId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Lines).WithOne().HasForeignKey(l => l.GoodsReceiptId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class GoodsReceiptLineConfig : IEntityTypeConfiguration<GoodsReceiptLine>
{
    public void Configure(EntityTypeBuilder<GoodsReceiptLine> b)
    {
        b.ToTable("proc_goods_receipt_lines");
        b.Property(x => x.Quantity).HasPrecision(18, 4);
        b.Property(x => x.UnitCost).HasPrecision(18, 4);
        b.HasOne(x => x.Item).WithMany().HasForeignKey(x => x.ItemId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Batch).WithMany().HasForeignKey(x => x.BatchId).OnDelete(DeleteBehavior.Restrict);
    }
}
