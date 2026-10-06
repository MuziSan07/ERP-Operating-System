using Erpos.Application.Common;
using Erpos.Domain.Entities;
using Erpos.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Erpos.Application.Inventory;

/// <summary>Result of taking stock out: which batches were used and what it cost at weighted average.</summary>
public record StockOut(decimal Value, decimal UnitCost, IReadOnlyList<(Guid? BatchId, decimal Quantity)> Batches);

/// <summary>
/// Moves stock and keeps the weighted average per item per warehouse:
///   in:  Value += qty × cost;  average = Value / Quantity
///   out: at the current average (the last unit out takes the remaining value, so no residue is left).
/// Batch-tracked items keep per-batch quantities; issues pick the earliest-expiring batch first and never expired stock.
/// Every change writes an immutable <see cref="StockMovement"/>. Callers save (one transaction with the journal).
/// </summary>
public class StockEngine(IAppDbContext db, ICurrentUser currentUser)
{
    private readonly Dictionary<(Guid, Guid), StockLevel> _levels = [];
    private readonly Dictionary<(Guid, Guid, Guid), StockBatchLevel> _batchLevels = [];

    private async Task<StockLevel> LevelAsync(Guid itemId, Guid warehouseId, CancellationToken ct)
    {
        if (_levels.TryGetValue((itemId, warehouseId), out var cached)) return cached;
        var level = await db.StockLevels.FirstOrDefaultAsync(l => l.ItemId == itemId && l.WarehouseId == warehouseId, ct);
        if (level == null)
        {
            level = new StockLevel { TenantId = currentUser.TenantId!.Value, ItemId = itemId, WarehouseId = warehouseId };
            db.StockLevels.Add(level);
        }
        return _levels[(itemId, warehouseId)] = level;
    }

    private async Task<StockBatchLevel> BatchLevelAsync(Guid itemId, Guid warehouseId, Guid batchId, CancellationToken ct)
    {
        if (_batchLevels.TryGetValue((itemId, warehouseId, batchId), out var cached)) return cached;
        var level = await db.StockBatchLevels.FirstOrDefaultAsync(l => l.ItemId == itemId && l.WarehouseId == warehouseId && l.BatchId == batchId, ct);
        if (level == null)
        {
            level = new StockBatchLevel { TenantId = currentUser.TenantId!.Value, ItemId = itemId, WarehouseId = warehouseId, BatchId = batchId };
            db.StockBatchLevels.Add(level);
        }
        return _batchLevels[(itemId, warehouseId, batchId)] = level;
    }

    /// <summary>Finds or creates a batch. Required for batch-tracked items; expiry required when the item tracks expiry.</summary>
    public async Task<Guid?> ResolveBatchAsync(Item item, string? batchNo, DateOnly? expiry, CancellationToken ct)
    {
        if (!item.TrackBatches) return null;
        var no = batchNo?.Trim();
        if (string.IsNullOrEmpty(no)) throw new ValidationException($"{item.Code} {item.Name} needs a batch number.");
        if (item.TrackExpiry && expiry == null) throw new ValidationException($"{item.Code} {item.Name} batch {no} needs an expiry date.");
        var batch = db.StockBatches.Local.FirstOrDefault(b => b.ItemId == item.Id && b.BatchNo == no)
                    ?? await db.StockBatches.FirstOrDefaultAsync(b => b.ItemId == item.Id && b.BatchNo == no, ct);
        if (batch == null)
        {
            batch = new StockBatch { TenantId = currentUser.TenantId!.Value, ItemId = item.Id, BatchNo = no, ExpiryDate = expiry };
            db.StockBatches.Add(batch);
        }
        else if (expiry != null && batch.ExpiryDate != null && batch.ExpiryDate != expiry)
            throw new ValidationException($"Batch {no} of {item.Code} already exists with expiry {batch.ExpiryDate:dd MMM yyyy}.");
        return batch.Id;
    }

    public async Task InAsync(Item item, Guid warehouseId, Guid entityId, Guid? batchId, decimal quantity, decimal unitCost, DateOnly date,
        StockMovementType type, Guid sourceId, string? reference, CancellationToken ct, decimal? exactValue = null)
    {
        if (item.Type != ItemType.Stock) return;
        if (quantity <= 0) throw new ValidationException("Quantity must be positive.");
        if (unitCost < 0) throw new ValidationException("Unit cost can't be negative.");
        var level = await LevelAsync(item.Id, warehouseId, ct);
        // Transfers pass the exact value taken out at the source so nothing is lost to rounding.
        var value = exactValue ?? Math.Round(quantity * unitCost, 2, MidpointRounding.AwayFromZero);
        level.Quantity += quantity;
        level.Value += value;
        level.Version++;
        if (batchId is { } b) (await BatchLevelAsync(item.Id, warehouseId, b, ct)).Quantity += quantity;
        Record(item, warehouseId, entityId, batchId, type, quantity, unitCost, value, level, date, sourceId, reference);
    }

    public async Task<StockOut> OutAsync(Item item, Guid warehouseId, Guid entityId, Guid? batchId, decimal quantity, DateOnly date,
        StockMovementType type, Guid sourceId, string? reference, CancellationToken ct, bool allowExpired = false)
    {
        if (item.Type != ItemType.Stock) throw new ValidationException($"{item.Name} is a service and has no stock.");
        if (quantity <= 0) throw new ValidationException("Quantity must be positive.");
        var level = await LevelAsync(item.Id, warehouseId, ct);
        if (level.Quantity < quantity)
            throw new ValidationException($"Only {level.Quantity:0.####} {item.Unit} of {item.Code} {item.Name} in stock; {quantity:0.####} requested.");

        var picks = item.TrackBatches ? await PickBatchesAsync(item, warehouseId, batchId, quantity, date, allowExpired, ct) : [(null, quantity)];
        var average = level.Value / level.Quantity;
        var totalValue = 0m;
        foreach (var (batch, qty) in picks)
        {
            var value = qty == level.Quantity ? level.Value : Math.Round(qty * average, 2, MidpointRounding.AwayFromZero);
            level.Quantity -= qty;
            level.Value -= value;
            totalValue += value;
            if (batch is { } b) (await BatchLevelAsync(item.Id, warehouseId, b, ct)).Quantity -= qty;
            Record(item, warehouseId, entityId, batch, type, -qty, average, -value, level, date, sourceId, reference);
        }
        level.Version++;
        return new StockOut(totalValue, Math.Round(average, 4), picks);
    }

    /// <summary>First-expiry-first-out across batches with stock; a named batch must have enough and not be expired.</summary>
    private async Task<List<(Guid?, decimal)>> PickBatchesAsync(Item item, Guid warehouseId, Guid? batchId, decimal quantity, DateOnly date,
        bool allowExpired, CancellationToken ct)
    {
        var levels = await db.StockBatchLevels.Where(l => l.ItemId == item.Id && l.WarehouseId == warehouseId && l.Quantity > 0)
            .Include(l => l.Batch).ToListAsync(ct);
        // Include pending changes from earlier lines of the same transaction.
        foreach (var l in levels.ToList())
            if (_batchLevels.TryGetValue((item.Id, warehouseId, l.BatchId), out var tracked)) levels[levels.IndexOf(l)] = tracked;
        levels = levels.Where(l => l.Quantity > 0).ToList();
        foreach (var l in levels) l.Batch ??= await db.StockBatches.FirstAsync(b => b.Id == l.BatchId, ct);

        if (batchId != null)
        {
            var chosen = levels.FirstOrDefault(l => l.BatchId == batchId) ?? throw new ValidationException($"That batch of {item.Code} has no stock here.");
            if (!allowExpired && chosen.Batch!.ExpiryDate is { } exp && exp < date) throw new ValidationException($"Batch {chosen.Batch.BatchNo} expired on {exp:dd MMM yyyy}.");
            if (chosen.Quantity < quantity) throw new ValidationException($"Batch {chosen.Batch!.BatchNo} has only {chosen.Quantity:0.####}.");
            return [(batchId, quantity)];
        }

        var usable = levels.Where(l => allowExpired || l.Batch!.ExpiryDate is not { } e || e >= date)
            .OrderBy(l => l.Batch!.ExpiryDate ?? DateOnly.MaxValue).ThenBy(l => l.Batch!.CreatedAt).ToList();
        if (usable.Sum(l => l.Quantity) < quantity)
            throw new ValidationException($"Not enough unexpired stock of {item.Code} {item.Name}: {usable.Sum(l => l.Quantity):0.####} available. Write off expired batches with an adjustment.");
        var result = new List<(Guid?, decimal)>();
        var remaining = quantity;
        foreach (var l in usable)
        {
            if (remaining <= 0) break;
            var take = Math.Min(remaining, l.Quantity);
            result.Add((l.BatchId, take));
            remaining -= take;
        }
        return result;
    }

    public async Task<decimal> AverageCostAsync(Guid itemId, Guid warehouseId, CancellationToken ct)
    {
        var level = await LevelAsync(itemId, warehouseId, ct);
        return level.Quantity > 0 ? level.Value / level.Quantity : 0;
    }

    public async Task<decimal> QuantityAsync(Guid itemId, Guid warehouseId, Guid? batchId, CancellationToken ct) =>
        batchId is { } b ? (await BatchLevelAsync(itemId, warehouseId, b, ct)).Quantity : (await LevelAsync(itemId, warehouseId, ct)).Quantity;

    private void Record(Item item, Guid warehouseId, Guid entityId, Guid? batchId, StockMovementType type, decimal qty, decimal unitCost,
        decimal value, StockLevel level, DateOnly date, Guid sourceId, string? reference) =>
        db.StockMovements.Add(new StockMovement
        {
            TenantId = currentUser.TenantId!.Value, EntityId = entityId, Date = date, ItemId = item.Id, WarehouseId = warehouseId, BatchId = batchId,
            Type = type, Quantity = qty, UnitCost = Math.Round(unitCost, 4), Value = value, QuantityAfter = level.Quantity,
            AverageCostAfter = level.Quantity > 0 ? Math.Round(level.Value / level.Quantity, 4) : 0, Reference = reference, SourceId = sourceId,
            CreatedBy = currentUser.UserId
        });
}
