using Erpos.Application.Authorization;
using Erpos.Application.Common;
using Erpos.Application.Dtos;
using Erpos.Application.Finance;
using Erpos.Domain.Entities;
using Erpos.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Erpos.Application.Inventory;

/// <summary>Items, warehouses, stock transactions (issue / transfer / adjustment / opening) and stock reports.</summary>
public class InventoryService(IAppDbContext db, IAccessService access, ICurrentUser currentUser, LedgerService ledger, StockEngine engine)
{
    private async Task EnsureInventoryUserAsync(CancellationToken ct)
    {
        var mine = await access.CurrentAsync(ct);
        if (!mine.ByEntity.Values.Any(s => s.Any(p => p.StartsWith("inventory.") || p.StartsWith("procurement."))))
            throw new ForbiddenException();
    }

    // ---------------- Categories & items ----------------

    public async Task<List<ItemCategoryDto>> CategoriesAsync(CancellationToken ct)
    {
        await EnsureInventoryUserAsync(ct);
        return await db.ItemCategories.OrderBy(c => c.Name).Select(c => new ItemCategoryDto(c.Id, c.Code, c.Name)).ToListAsync(ct);
    }

    public async Task<ItemCategoryDto> SaveCategoryAsync(Guid? id, SaveItemCategoryRequest req, CancellationToken ct)
    {
        await access.EnsureAnywhereAsync(Permissions.ItemsCreate, ct);
        var c = id == null ? null : await db.ItemCategories.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Category");
        if (c == null) { c = new ItemCategory { TenantId = currentUser.TenantId!.Value }; db.ItemCategories.Add(c); }
        var code = Guard.Code(req.Code);
        if (await db.ItemCategories.AnyAsync(x => x.Code == code && x.Id != c.Id, ct)) throw new ValidationException($"Category {code} exists.");
        c.Code = code;
        c.Name = Guard.Required(req.Name, "Name", 100);
        await db.SaveChangesAsync(ct);
        return new ItemCategoryDto(c.Id, c.Code, c.Name);
    }

    public async Task<List<ItemDto>> ItemsAsync(string? search, Guid? categoryId, bool activeOnly, CancellationToken ct)
    {
        await EnsureInventoryUserAsync(ct);
        var warehouses = await VisibleWarehousesAsync(Permissions.StockView, ct);
        var q = db.Items.AsQueryable();
        if (activeOnly) q = q.Where(i => i.IsActive);
        if (categoryId != null) q = q.Where(i => i.CategoryId == categoryId);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            q = q.Where(i => i.Name.ToLower().Contains(s) || i.Code.ToLower().Contains(s) || (i.Barcode != null && i.Barcode == s));
        }
        return await q.OrderBy(i => i.Code).Select(i => new ItemDto(i.Id, i.Code, i.Name, i.CategoryId, i.Category == null ? null : i.Category.Name,
            i.Type, i.Unit, i.Barcode, i.TrackBatches, i.TrackExpiry, i.ReorderLevel, i.StandardCost, i.InventoryAccountId, i.ConsumptionAccountId,
            i.PurchaseTaxRateId, i.IsActive,
            db.StockLevels.Where(l => l.ItemId == i.Id && warehouses.Contains(l.WarehouseId)).Sum(l => l.Quantity),
            db.StockLevels.Where(l => l.ItemId == i.Id && warehouses.Contains(l.WarehouseId)).Sum(l => l.Value))).ToListAsync(ct);
    }

    public async Task<ItemDto> SaveItemAsync(Guid? id, SaveItemRequest req, CancellationToken ct)
    {
        await access.EnsureAnywhereAsync(id == null ? Permissions.ItemsCreate : Permissions.ItemsEdit, ct);
        var item = id == null ? null : await db.Items.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Item");
        var hasMovements = item != null && await db.StockMovements.AnyAsync(m => m.ItemId == item.Id, ct);
        if (item == null) { item = new Item { TenantId = currentUser.TenantId!.Value }; db.Items.Add(item); }

        var code = Guard.Code(req.Code, "Item code");
        if (await db.Items.AnyAsync(x => x.Code == code && x.Id != item.Id, ct)) throw new ValidationException($"Item code {code} already exists.");
        if (hasMovements && (req.Type != item.Type || req.TrackBatches != item.TrackBatches))
            throw new ValidationException("This item has stock history; its type and batch tracking can't change.");
        if (req.TrackExpiry && !req.TrackBatches) throw new ValidationException("Expiry tracking needs batch tracking.");
        if (req.Type == ItemType.Service && req.TrackBatches) throw new ValidationException("Services don't have batches.");

        item.Code = code;
        item.Name = Guard.Required(req.Name, "Name");
        item.CategoryId = req.CategoryId;
        item.Type = req.Type;
        item.Unit = Guard.Required(req.Unit, "Unit", 20);
        item.Barcode = string.IsNullOrWhiteSpace(req.Barcode) ? null : req.Barcode.Trim();
        item.TrackBatches = req.TrackBatches;
        item.TrackExpiry = req.TrackExpiry;
        item.ReorderLevel = Math.Max(0, req.ReorderLevel);
        item.StandardCost = req.StandardCost;
        item.InventoryAccountId = req.InventoryAccountId;
        item.ConsumptionAccountId = req.ConsumptionAccountId;
        item.PurchaseTaxRateId = req.PurchaseTaxRateId;
        item.IsActive = req.IsActive;
        await db.SaveChangesAsync(ct);
        return (await ItemsAsync(item.Code, null, false, ct)).First(i => i.Id == item.Id);
    }

    // ---------------- Warehouses ----------------

    private async Task<HashSet<Guid>> VisibleWarehousesAsync(string permission, CancellationToken ct)
    {
        var entities = await access.EntitiesWithAsync(permission, ct);
        return (await db.Warehouses.Where(w => entities.Contains(w.EntityId)).Select(w => w.Id).ToListAsync(ct)).ToHashSet();
    }

    public async Task<List<WarehouseDto>> WarehousesAsync(CancellationToken ct)
    {
        await EnsureInventoryUserAsync(ct);
        // Anyone receiving, ordering or moving stock needs to pick warehouses they work with.
        var mine = await access.CurrentAsync(ct);
        var entities = mine.ByEntity.Where(kv => kv.Value.Any(p => p.StartsWith("inventory.") || p.StartsWith("procurement."))).Select(kv => kv.Key).ToHashSet();
        return await db.Warehouses.Where(w => entities.Contains(w.EntityId)).OrderBy(w => w.Entity!.Depth).ThenBy(w => w.Name)
            .Select(w => new WarehouseDto(w.Id, w.EntityId, w.Entity!.Name, w.Code, w.Name, w.Address, w.IsActive,
                db.StockLevels.Where(l => l.WarehouseId == w.Id).Sum(l => l.Value))).ToListAsync(ct);
    }

    public async Task<WarehouseDto> SaveWarehouseAsync(Guid? id, SaveWarehouseRequest req, CancellationToken ct)
    {
        var w = id == null ? null : await db.Warehouses.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Warehouse");
        if (w == null)
        {
            await access.EnsureAsync(Permissions.WarehousesCreate, req.EntityId, ct);
            w = new Warehouse { TenantId = currentUser.TenantId!.Value };
            db.Warehouses.Add(w);
        }
        else
        {
            await access.EnsureAsync(Permissions.WarehousesEdit, w.EntityId, ct);
            if (req.EntityId != w.EntityId && await db.StockLevels.AnyAsync(l => l.WarehouseId == w.Id && l.Quantity != 0, ct))
                throw new ValidationException("Move the stock out before assigning this warehouse to another branch.");
        }
        var code = Guard.Code(req.Code);
        if (await db.Warehouses.AnyAsync(x => x.Code == code && x.Id != w.Id, ct)) throw new ValidationException($"Warehouse code {code} exists.");
        w.EntityId = req.EntityId;
        w.Code = code;
        w.Name = Guard.Required(req.Name, "Name", 150);
        w.Address = req.Address;
        w.IsActive = req.IsActive;
        await db.SaveChangesAsync(ct);
        return (await WarehousesAsync(ct)).First(x => x.Id == w.Id);
    }

    // ---------------- Stock transactions ----------------

    /// <param name="system">True when another module (e.g. a hotel minibar charge) acts with its own permission check.</param>
    public async Task<StockTransactionDto> CreateTransactionAsync(CreateStockTransactionRequest req, CancellationToken ct, bool system = false)
    {
        var from = await db.Warehouses.FirstOrDefaultAsync(w => w.Id == req.WarehouseId && w.IsActive, ct) ?? throw new NotFoundException("Warehouse");
        var permission = req.Type switch
        {
            StockTransactionType.Issue => Permissions.StockIssue,
            StockTransactionType.Transfer => Permissions.StockTransfer,
            _ => Permissions.StockAdjust
        };
        if (!system) await access.EnsureAsync(permission, from.EntityId, ct);
        Warehouse? to = null;
        if (req.Type == StockTransactionType.Transfer)
        {
            to = await db.Warehouses.FirstOrDefaultAsync(w => w.Id == req.ToWarehouseId && w.IsActive, ct) ?? throw new ValidationException("Choose the destination warehouse.");
            if (to.Id == from.Id) throw new ValidationException("Source and destination are the same warehouse.");
            await access.EnsureAsync(Permissions.StockTransfer, to.EntityId, ct);
        }
        if (!system && req.ChargeEntityId is { } charge && charge != from.EntityId) await access.EnsureAsync(permission, charge, ct);
        if (req.Lines.Count == 0) throw new ValidationException("Add at least one line.");
        await ledger.EnsureOpenPeriodAsync(req.Date, ct);

        var settings = await ledger.SettingsAsync(ct);
        var itemIds = req.Lines.Select(l => l.ItemId).Distinct().ToList();
        var items = await db.Items.Where(i => itemIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id, ct);
        var prefix = req.Type switch { StockTransactionType.Issue => "ISS", StockTransactionType.Transfer => "TRF", StockTransactionType.Opening => "OPN", _ => "ADJ" };
        var tx = new StockTransaction
        {
            TenantId = currentUser.TenantId!.Value, EntityId = from.EntityId, Type = req.Type, Date = req.Date, WarehouseId = from.Id,
            ToWarehouseId = to?.Id, AccountId = req.AccountId, ChargeEntityId = req.ChargeEntityId, Reference = req.Reference, Notes = req.Notes,
            Number = await ledger.NextNumberAsync(prefix, req.Date, ct)
        };

        Guid InvAccount(Item i) => i.InventoryAccountId ?? settings.DefaultInventoryAccountId ?? throw new ValidationException("Set the default inventory account.");
        var journal = new List<LineInput>();
        foreach (var l in req.Lines)
        {
            if (!items.TryGetValue(l.ItemId, out var item)) throw new NotFoundException("Item");
            if (item.Type != ItemType.Stock) throw new ValidationException($"{item.Name} is a service, not a stock item.");
            if (l.Quantity == 0) throw new ValidationException("Quantities can't be zero.");
            var line = new StockTransactionLine { StockTransactionId = tx.Id, ItemId = item.Id, Notes = l.Notes };

            switch (req.Type)
            {
                case StockTransactionType.Issue:
                {
                    if (l.Quantity < 0) throw new ValidationException("Issue quantities are positive.");
                    var o = await engine.OutAsync(item, from.Id, from.EntityId, l.BatchId, l.Quantity, req.Date, StockMovementType.Issue, tx.Id, tx.Number, ct);
                    var expense = req.AccountId ?? item.ConsumptionAccountId ?? settings.DefaultConsumptionAccountId ?? throw new ValidationException("Set a consumption account.");
                    journal.Add(new(expense, o.Value, 0, req.ChargeEntityId ?? from.EntityId, $"{item.Code} {item.Name} × {l.Quantity:0.####}"));
                    journal.Add(new(InvAccount(item), 0, o.Value, from.EntityId, $"{item.Code} issued"));
                    Fill(line, l.Quantity, o, l.BatchId);
                    break;
                }
                case StockTransactionType.Transfer:
                {
                    if (l.Quantity < 0) throw new ValidationException("Transfer quantities are positive.");
                    var o = await engine.OutAsync(item, from.Id, from.EntityId, l.BatchId, l.Quantity, req.Date, StockMovementType.TransferOut, tx.Id, tx.Number, ct);
                    var moved = 0m;
                    for (var b = 0; b < o.Batches.Count; b++)
                    {
                        var (batch, qty) = o.Batches[b];
                        // The last batch takes the remainder so the destination receives exactly the value that left the source.
                        var part = b == o.Batches.Count - 1 ? o.Value - moved : Math.Round(o.Value * qty / l.Quantity, 2);
                        moved += part;
                        await engine.InAsync(item, to!.Id, to.EntityId, batch, qty, o.UnitCost, req.Date, StockMovementType.TransferIn, tx.Id, tx.Number, ct, part);
                    }
                    if (to!.EntityId != from.EntityId)
                    {
                        journal.Add(new(InvAccount(item), o.Value, 0, to.EntityId, $"{item.Code} transferred in"));
                        journal.Add(new(InvAccount(item), 0, o.Value, from.EntityId, $"{item.Code} transferred out"));
                    }
                    Fill(line, l.Quantity, o, l.BatchId);
                    break;
                }
                default: // Adjustment / Opening
                {
                    var offset = req.AccountId ?? (req.Type == StockTransactionType.Opening ? settings.RetainedEarningsAccountId : settings.InventoryAdjustmentAccountId)
                                 ?? throw new ValidationException("Choose the offset account.");
                    if (l.Quantity > 0)
                    {
                        var cost = l.UnitCost ?? (req.Type == StockTransactionType.Opening ? null : await engine.AverageCostAsync(item.Id, from.Id, ct));
                        if (cost is null or <= 0) cost = l.UnitCost ?? item.StandardCost;
                        if (cost is null or < 0) throw new ValidationException($"Enter a unit cost for {item.Code} {item.Name}.");
                        var batch = l.BatchId ?? await engine.ResolveBatchAsync(item, l.BatchNo, l.ExpiryDate, ct);
                        await engine.InAsync(item, from.Id, from.EntityId, batch, l.Quantity, cost.Value, req.Date,
                            req.Type == StockTransactionType.Opening ? StockMovementType.Opening : StockMovementType.AdjustmentIn, tx.Id, tx.Number, ct);
                        var value = Math.Round(l.Quantity * cost.Value, 2, MidpointRounding.AwayFromZero);
                        journal.Add(new(InvAccount(item), value, 0, from.EntityId, $"{item.Code} +{l.Quantity:0.####}"));
                        journal.Add(new(offset, 0, value, from.EntityId, l.Notes ?? (req.Type == StockTransactionType.Opening ? "Opening stock" : "Stock count gain")));
                        line.BatchId = batch;
                        line.Quantity = l.Quantity;
                        line.UnitCost = cost.Value;
                        line.Value = value;
                    }
                    else
                    {
                        if (req.Type == StockTransactionType.Opening) throw new ValidationException("Opening stock quantities are positive.");
                        // Write-offs may remove expired batches.
                        var o = await engine.OutAsync(item, from.Id, from.EntityId, l.BatchId, -l.Quantity, req.Date, StockMovementType.AdjustmentOut, tx.Id, tx.Number, ct, allowExpired: true);
                        journal.Add(new(offset, o.Value, 0, from.EntityId, l.Notes ?? "Stock count loss / write-off"));
                        journal.Add(new(InvAccount(item), 0, o.Value, from.EntityId, $"{item.Code} {l.Quantity:0.####}"));
                        Fill(line, l.Quantity, o, l.BatchId);
                        line.Value = -o.Value;
                    }
                    break;
                }
            }
            tx.Lines.Add(line);
        }

        tx.TotalValue = tx.Lines.Sum(l => Math.Abs(l.Value));
        var postable = journal.Where(j => j.Debit > 0 || j.Credit > 0).ToList();
        if (postable.Count >= 2)
        {
            var entry = await ledger.BuildAndPostAsync(from.EntityId, req.Date, $"{TypeLabel(req.Type)} {tx.Number} — {from.Name}", settings.BaseCurrency, 1,
                JournalSource.Inventory, tx.Id, postable, tx.Number, ct);
            tx.JournalEntryId = entry.Id;
        }
        db.StockTransactions.Add(tx);
        await db.SaveChangesAsync(ct);
        return system ? await LoadTransactionAsync(tx.Id, ct) : await GetTransactionAsync(tx.Id, ct);
    }

    private static void Fill(StockTransactionLine line, decimal qty, StockOut o, Guid? batchId)
    {
        line.Quantity = qty;
        line.UnitCost = o.UnitCost;
        line.Value = o.Value;
        line.BatchId = batchId ?? (o.Batches.Count == 1 ? o.Batches[0].BatchId : null);
    }

    private static string TypeLabel(StockTransactionType t) => t switch
    {
        StockTransactionType.Issue => "Stock issue", StockTransactionType.Transfer => "Stock transfer",
        StockTransactionType.Opening => "Opening stock", _ => "Stock adjustment"
    };

    public async Task<PagedResult<StockTransactionDto>> TransactionsAsync(StockTransactionType? type, Guid? warehouseId, int page, int pageSize, CancellationToken ct)
    {
        var visible = await VisibleWarehousesAsync(Permissions.StockView, ct);
        var q = db.StockTransactions.Where(t => visible.Contains(t.WarehouseId) || (t.ToWarehouseId != null && visible.Contains(t.ToWarehouseId.Value)));
        if (type != null) q = q.Where(t => t.Type == type);
        if (warehouseId != null) q = q.Where(t => t.WarehouseId == warehouseId || t.ToWarehouseId == warehouseId);
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 200);
        var total = await q.CountAsync(ct);
        var ids = await q.OrderByDescending(t => t.Date).ThenByDescending(t => t.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).Select(t => t.Id).ToListAsync(ct);
        var list = new List<StockTransactionDto>();
        foreach (var id in ids) list.Add(await LoadTransactionAsync(id, ct));
        return new PagedResult<StockTransactionDto>(list, total, page, pageSize);
    }

    public async Task<StockTransactionDto> GetTransactionAsync(Guid id, CancellationToken ct)
    {
        var dto = await LoadTransactionAsync(id, ct);
        var visible = await VisibleWarehousesAsync(Permissions.StockView, ct);
        if (!visible.Contains(dto.WarehouseId) && !(dto.ToWarehouseId is { } t && visible.Contains(t))) throw new ForbiddenException();
        return dto;
    }

    private async Task<StockTransactionDto> LoadTransactionAsync(Guid id, CancellationToken ct)
    {
        var t = await db.StockTransactions.Include(x => x.Warehouse).Include(x => x.ToWarehouse)
                    .Include(x => x.Lines).ThenInclude(l => l.Item).Include(x => x.Lines).ThenInclude(l => l.Batch)
                    .FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Stock transaction");
        var account = t.AccountId == null ? null : await db.Accounts.Where(a => a.Id == t.AccountId).Select(a => a.Code + " " + a.Name).FirstOrDefaultAsync(ct);
        var by = t.CreatedBy == null ? null : await db.Users.Where(u => u.Id == t.CreatedBy).Select(u => u.FullName).FirstOrDefaultAsync(ct);
        return new StockTransactionDto(t.Id, t.Type, t.Number, t.Date, t.WarehouseId, t.Warehouse!.Name, t.ToWarehouseId, t.ToWarehouse?.Name, account,
            t.Reference, t.Notes, t.TotalValue, t.JournalEntryId, by,
            t.Lines.Select(l => new StockTransactionLineDto(l.ItemId, l.Item!.Code, l.Item.Name, l.Item.Unit, l.Batch?.BatchNo, l.Batch?.ExpiryDate,
                l.Quantity, l.UnitCost, l.Value, l.Notes)).ToList());
    }

    // ---------------- Reports ----------------

    public async Task<List<StockRow>> StockOnHandAsync(Guid? warehouseId, string? search, bool belowReorderOnly, CancellationToken ct)
    {
        var visible = await VisibleWarehousesAsync(Permissions.StockView, ct);
        var q = db.StockLevels.Where(l => visible.Contains(l.WarehouseId) && (l.Quantity != 0 || l.Item!.ReorderLevel > 0));
        if (warehouseId != null) q = q.Where(l => l.WarehouseId == warehouseId);
        if (!string.IsNullOrWhiteSpace(search)) q = q.Where(l => l.Item!.Name.Contains(search) || l.Item.Code.Contains(search));
        var rows = await q.Select(l => new { l.ItemId, l.Item!.Code, l.Item.Name, l.Item.Unit, Category = l.Item.Category == null ? null : l.Item.Category.Name,
            l.WarehouseId, Warehouse = l.Warehouse!.Name, l.Quantity, l.Value, l.Item.ReorderLevel }).ToListAsync(ct);
        return rows.Select(r => new StockRow(r.ItemId, r.Code, r.Name, r.Unit, r.Category, r.WarehouseId, r.Warehouse, r.Quantity,
                r.Quantity > 0 ? Math.Round(r.Value / r.Quantity, 4) : 0, r.Value, r.ReorderLevel, r.ReorderLevel > 0 && r.Quantity <= r.ReorderLevel))
            .Where(r => !belowReorderOnly || r.BelowReorder).OrderBy(r => r.ItemCode).ThenBy(r => r.WarehouseName).ToList();
    }

    /// <summary>Batch quantities with days to expiry (negative = expired), soonest first.</summary>
    public async Task<List<BatchStockRow>> BatchesAsync(Guid? warehouseId, Guid? itemId, int? expiringWithinDays, CancellationToken ct)
    {
        var visible = await VisibleWarehousesAsync(Permissions.StockView, ct);
        var q = db.StockBatchLevels.Where(l => visible.Contains(l.WarehouseId) && l.Quantity > 0);
        if (warehouseId != null) q = q.Where(l => l.WarehouseId == warehouseId);
        if (itemId != null) q = q.Where(l => l.ItemId == itemId);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var rows = await q.Select(l => new
        {
            l.ItemId, l.WarehouseId, l.BatchId, l.Quantity, l.Batch!.BatchNo, l.Batch.ExpiryDate,
            Item = db.Items.Where(i => i.Id == l.ItemId).Select(i => new { i.Code, i.Name }).First(),
            Warehouse = db.Warehouses.Where(w => w.Id == l.WarehouseId).Select(w => w.Name).First()
        }).ToListAsync(ct);
        return rows.Select(r => new BatchStockRow(r.ItemId, r.Item.Code, r.Item.Name, r.WarehouseId, r.Warehouse, r.BatchId, r.BatchNo, r.ExpiryDate,
                r.ExpiryDate is { } e ? e.DayNumber - today.DayNumber : null, r.Quantity))
            .Where(r => expiringWithinDays == null || (r.DaysToExpiry != null && r.DaysToExpiry <= expiringWithinDays))
            .OrderBy(r => r.DaysToExpiry ?? int.MaxValue).ThenBy(r => r.ItemCode).ToList();
    }

    public async Task<List<MovementRow>> MovementsAsync(Guid? itemId, Guid? warehouseId, DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        var visible = await VisibleWarehousesAsync(Permissions.StockView, ct);
        var q = db.StockMovements.Where(m => visible.Contains(m.WarehouseId));
        if (itemId != null) q = q.Where(m => m.ItemId == itemId);
        if (warehouseId != null) q = q.Where(m => m.WarehouseId == warehouseId);
        if (from != null) q = q.Where(m => m.Date >= from);
        if (to != null) q = q.Where(m => m.Date <= to);
        return await q.OrderByDescending(m => m.Date).ThenByDescending(m => m.CreatedAt).Take(1000)
            .Select(m => new MovementRow(m.Date, m.Type, m.Item!.Code, m.Item.Name, m.Warehouse!.Name, m.Batch == null ? null : m.Batch.BatchNo,
                m.Quantity, m.UnitCost, m.Value, m.QuantityAfter, m.AverageCostAfter, m.Reference, m.SourceId)).ToListAsync(ct);
    }

    /// <summary>Stock value per the stock ledger vs the inventory accounts in the general ledger — they should agree.</summary>
    public async Task<StockValuationDto> ValuationAsync(CancellationToken ct)
    {
        await access.EnsureAtRootAsync(Permissions.StockView, ct);
        var settings = await ledger.SettingsAsync(ct);
        var rows = await StockOnHandAsync(null, null, false, ct);
        var inventoryAccounts = await db.Accounts.Where(a => a.SubType == AccountSubType.Inventory).Select(a => a.Id).ToListAsync(ct);
        var ledgerValue = await db.JournalLines.Where(l => inventoryAccounts.Contains(l.AccountId) &&
                (l.JournalEntry!.Status == JournalStatus.Posted || l.JournalEntry.Status == JournalStatus.Reversed))
            .SumAsync(l => l.BaseDebit - l.BaseCredit, ct);
        var stockValue = rows.Sum(r => r.Value);
        return new StockValuationDto(settings.BaseCurrency, stockValue, ledgerValue, stockValue - ledgerValue, rows.Where(r => r.Quantity != 0).ToList());
    }
}
