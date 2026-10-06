using Erpos.Application.Authorization;
using Erpos.Application.Common;
using Erpos.Application.Dtos;
using Erpos.Application.Finance;
using Erpos.Domain.Entities;
using Erpos.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Erpos.Application.Inventory;

/// <summary>
/// Purchase request → purchase order → goods received note → vendor bill (three-way matched in <see cref="DocumentService"/>).
/// A GRN brings stock in at the PO price and accrues "goods received not invoiced"; the bill clears that accrual.
/// </summary>
public class ProcurementService(IAppDbContext db, IAccessService access, ICurrentUser currentUser, LedgerService ledger, StockEngine engine)
{
    // ======================= Purchase requests =======================

    public async Task<PagedResult<PurchaseRequestDto>> RequestsAsync(PurchaseRequestStatus? status, bool mine, int page, int pageSize, CancellationToken ct)
    {
        var visible = await access.EntitiesWithAsync(Permissions.PrView, ct);
        var q = db.PurchaseRequests.Where(r => visible.Contains(r.EntityId) || r.CreatedBy == currentUser.UserId);
        if (mine) q = q.Where(r => r.CreatedBy == currentUser.UserId);
        if (status != null) q = q.Where(r => r.Status == status);
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 200);
        var total = await q.CountAsync(ct);
        var ids = await q.OrderByDescending(r => r.Date).ThenByDescending(r => r.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).Select(r => r.Id).ToListAsync(ct);
        var list = new List<PurchaseRequestDto>();
        foreach (var id in ids) list.Add(await LoadRequestAsync(id, ct));
        return new PagedResult<PurchaseRequestDto>(list, total, page, pageSize);
    }

    public async Task<PurchaseRequestDto> RequestAsync(Guid id, CancellationToken ct)
    {
        var dto = await LoadRequestAsync(id, ct);
        var createdBy = await db.PurchaseRequests.Where(r => r.Id == id).Select(r => r.CreatedBy).FirstAsync(ct);
        if (createdBy != currentUser.UserId) await access.EnsureAsync(Permissions.PrView, dto.EntityId, ct);
        return dto;
    }

    public async Task<PurchaseRequestDto> SaveRequestAsync(Guid? id, SavePurchaseRequestRequest req, CancellationToken ct)
    {
        await access.EnsureAsync(Permissions.PrCreate, req.EntityId, ct);
        PurchaseRequest pr;
        if (id == null)
        {
            pr = new PurchaseRequest { TenantId = currentUser.TenantId!.Value, Number = await ledger.NextNumberAsync("PR", req.Date, ct) };
            db.PurchaseRequests.Add(pr);
        }
        else
        {
            pr = await db.PurchaseRequests.Include(r => r.Lines).FirstOrDefaultAsync(r => r.Id == id, ct) ?? throw new NotFoundException("Purchase request");
            if (pr.CreatedBy != currentUser.UserId) await access.EnsureAsync(Permissions.PrEdit, pr.EntityId, ct);
            if (pr.Status is not (PurchaseRequestStatus.Draft or PurchaseRequestStatus.Rejected)) throw new ValidationException("Only draft or rejected requests can be edited.");
            db.PurchaseRequestLines.RemoveRange(pr.Lines);
            pr.Lines.Clear();
            pr.Status = PurchaseRequestStatus.Draft;
        }
        if (req.Lines.Count == 0) throw new ValidationException("Add at least one item.");
        var itemIds = req.Lines.Select(l => l.ItemId).ToList();
        if (await db.Items.CountAsync(i => itemIds.Contains(i.Id) && i.IsActive, ct) != itemIds.Distinct().Count()) throw new ValidationException("Unknown or inactive item.");

        pr.EntityId = req.EntityId;
        pr.Date = req.Date;
        pr.RequiredBy = req.RequiredBy;
        pr.Purpose = Guard.Required(req.Purpose, "Purpose", 500);
        foreach (var l in req.Lines)
        {
            if (l.Quantity <= 0) throw new ValidationException("Quantities must be positive.");
            pr.Lines.Add(new PurchaseRequestLine { PurchaseRequestId = pr.Id, ItemId = l.ItemId, Quantity = l.Quantity, EstimatedUnitPrice = Math.Max(0, l.EstimatedUnitPrice), Notes = l.Notes });
        }
        await db.SaveChangesAsync(ct);
        return await RequestAsync(pr.Id, ct);
    }

    public async Task<PurchaseRequestDto> SubmitRequestAsync(Guid id, CancellationToken ct)
    {
        var pr = await db.PurchaseRequests.FirstOrDefaultAsync(r => r.Id == id, ct) ?? throw new NotFoundException("Purchase request");
        if (pr.CreatedBy != currentUser.UserId) await access.EnsureAsync(Permissions.PrEdit, pr.EntityId, ct);
        if (pr.Status != PurchaseRequestStatus.Draft) throw new ValidationException("Only drafts can be submitted.");
        pr.Status = PurchaseRequestStatus.Submitted;
        await db.SaveChangesAsync(ct);
        return await RequestAsync(id, ct);
    }

    public async Task<PurchaseRequestDto> DecideRequestAsync(Guid id, DecisionRequest req, CancellationToken ct)
    {
        var pr = await db.PurchaseRequests.FirstOrDefaultAsync(r => r.Id == id, ct) ?? throw new NotFoundException("Purchase request");
        await access.EnsureAsync(Permissions.PrApprove, pr.EntityId, ct);
        if (pr.Status != PurchaseRequestStatus.Submitted) throw new ValidationException("Only submitted requests can be approved or rejected.");
        if (pr.CreatedBy == currentUser.UserId) throw new ForbiddenException("You can't approve your own purchase request.");
        if (!req.Approve && string.IsNullOrWhiteSpace(req.Comment)) throw new ValidationException("Give a reason when rejecting.");
        pr.Status = req.Approve ? PurchaseRequestStatus.Approved : PurchaseRequestStatus.Rejected;
        pr.ApprovedBy = currentUser.UserId;
        pr.ApprovedAt = DateTime.UtcNow;
        pr.DecisionComment = req.Comment;
        await db.SaveChangesAsync(ct);
        return await RequestAsync(id, ct);
    }

    public async Task<PurchaseRequestDto> CancelRequestAsync(Guid id, CancellationToken ct)
    {
        var pr = await db.PurchaseRequests.Include(r => r.Lines).FirstOrDefaultAsync(r => r.Id == id, ct) ?? throw new NotFoundException("Purchase request");
        if (pr.CreatedBy != currentUser.UserId) await access.EnsureAsync(Permissions.PrEdit, pr.EntityId, ct);
        if (pr.Lines.Any(l => l.QuantityOrdered > 0) || pr.Status is PurchaseRequestStatus.Ordered or PurchaseRequestStatus.Cancelled)
            throw new ValidationException("This request is already (partly) ordered or cancelled.");
        pr.Status = PurchaseRequestStatus.Cancelled;
        await db.SaveChangesAsync(ct);
        return await RequestAsync(id, ct);
    }

    private async Task<PurchaseRequestDto> LoadRequestAsync(Guid id, CancellationToken ct)
    {
        var r = await db.PurchaseRequests.Include(x => x.Entity).Include(x => x.Lines).ThenInclude(l => l.Item)
                    .FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Purchase request");
        var names = await UserNamesAsync([r.CreatedBy, r.ApprovedBy], ct);
        return new PurchaseRequestDto(r.Id, r.Number, r.EntityId, r.Entity!.Name, r.Date, r.RequiredBy, r.Purpose, r.Status,
            Name(names, r.CreatedBy), Name(names, r.ApprovedBy), r.ApprovedAt, r.DecisionComment, r.Lines.Sum(l => l.Quantity * l.EstimatedUnitPrice),
            r.Lines.Select(l => new PrLineDto(l.Id, l.ItemId, l.Item!.Code, l.Item.Name, l.Item.Unit, l.Quantity, l.EstimatedUnitPrice, l.QuantityOrdered, l.Notes)).ToList());
    }

    // ======================= Purchase orders =======================

    public async Task<PagedResult<PurchaseOrderListItem>> OrdersAsync(PurchaseOrderStatus? status, Guid? vendorId, string? search, int page, int pageSize, CancellationToken ct)
    {
        var visible = await access.EntitiesWithAsync(Permissions.PoView, ct);
        var q = db.PurchaseOrders.Where(o => visible.Contains(o.EntityId));
        if (status != null) q = q.Where(o => o.Status == status);
        if (vendorId != null) q = q.Where(o => o.VendorId == vendorId);
        if (!string.IsNullOrWhiteSpace(search)) q = q.Where(o => o.Number.Contains(search) || o.Vendor!.Name.Contains(search));
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 200);
        var total = await q.CountAsync(ct);
        var rows = await q.OrderByDescending(o => o.Date).ThenByDescending(o => o.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(o => new
            {
                o.Id, o.Number, Entity = o.Entity!.Name, Vendor = o.Vendor!.Name, o.Date, o.ExpectedDate, Warehouse = o.Warehouse!.Name, o.Currency, o.Status, o.Total,
                Ordered = o.Lines.Sum(l => l.Quantity), Received = o.Lines.Sum(l => l.QuantityReceived), Billed = o.Lines.Sum(l => l.QuantityBilled)
            }).ToListAsync(ct);
        return new PagedResult<PurchaseOrderListItem>(rows.Select(o => new PurchaseOrderListItem(o.Id, o.Number, o.Entity, o.Vendor, o.Date, o.ExpectedDate,
            o.Warehouse, o.Currency, o.Status, o.Total, o.Ordered == 0 ? 0 : Math.Round(o.Received / o.Ordered * 100, 1),
            o.Ordered == 0 ? 0 : Math.Round(o.Billed / o.Ordered * 100, 1))).ToList(), total, page, pageSize);
    }

    public async Task<PurchaseOrderDto> OrderAsync(Guid id, CancellationToken ct)
    {
        var o = await db.PurchaseOrders.Include(x => x.Entity).Include(x => x.Vendor).Include(x => x.Warehouse)
                    .Include(x => x.Lines).ThenInclude(l => l.Item).Include(x => x.Lines).ThenInclude(l => l.TaxRate)
                    .FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Purchase order");
        await access.EnsureAsync(Permissions.PoView, o.EntityId, ct);
        var names = await UserNamesAsync([o.CreatedBy, o.ApprovedBy], ct);
        var receipts = await db.GoodsReceipts.Where(g => g.PurchaseOrderId == id).OrderBy(g => g.Date)
            .Select(g => new LinkedDocDto(g.Id, g.Number, g.Date, g.TotalValue, "Received")).ToListAsync(ct);
        var bills = await db.FinanceDocuments.Where(d => d.PurchaseOrderId == id).OrderBy(d => d.Date)
            .Select(d => new LinkedDocDto(d.Id, d.Number == "" ? null : d.Number, d.Date, d.Total, d.Status.ToString())).ToListAsync(ct);
        return new PurchaseOrderDto(o.Id, o.Number, o.EntityId, o.Entity!.Name, o.VendorId, o.Vendor!.Name, o.Vendor.Ntn, o.Vendor.Strn,
            string.Join(", ", new[] { o.Vendor.Address, o.Vendor.City }.Where(s => !string.IsNullOrEmpty(s))), o.Date, o.ExpectedDate, o.WarehouseId,
            o.Warehouse!.Name, o.Currency, o.ExchangeRate, o.Terms, o.Notes, o.Status, o.Subtotal, o.TaxTotal, o.Total, Name(names, o.CreatedBy),
            Name(names, o.ApprovedBy), o.ApprovedAt,
            o.Lines.OrderBy(l => l.SortOrder).Select(l => new PoLineDto(l.Id, l.ItemId, l.Item!.Code, l.Item.Name, l.Item.Unit, l.Item.Type, l.Item.TrackBatches,
                l.Item.TrackExpiry, l.Description, l.Quantity, l.UnitPrice, l.TaxRateId, l.TaxRate?.Name, l.Amount, l.TaxAmount, l.QuantityReceived,
                l.QuantityBilled, l.PurchaseRequestLineId)).ToList(), receipts, bills);
    }

    public async Task<PurchaseOrderDto> SaveOrderAsync(Guid? id, SavePurchaseOrderRequest req, CancellationToken ct)
    {
        await access.EnsureAsync(Permissions.PoCreate, req.EntityId, ct);
        PurchaseOrder po;
        if (id == null)
        {
            po = new PurchaseOrder { TenantId = currentUser.TenantId!.Value, Number = await ledger.NextNumberAsync("PO", req.Date, ct) };
            db.PurchaseOrders.Add(po);
        }
        else
        {
            po = await db.PurchaseOrders.Include(o => o.Lines).FirstOrDefaultAsync(o => o.Id == id, ct) ?? throw new NotFoundException("Purchase order");
            await access.EnsureAsync(Permissions.PoEdit, po.EntityId, ct);
            if (po.Status != PurchaseOrderStatus.Draft) throw new ValidationException("Only draft orders can be edited.");
            db.PurchaseOrderLines.RemoveRange(po.Lines);
            po.Lines.Clear();
        }

        var vendor = await db.Contacts.FirstOrDefaultAsync(c => c.Id == req.VendorId && c.IsVendor && c.IsActive, ct) ?? throw new ValidationException("Choose an active vendor.");
        var warehouse = await db.Warehouses.FirstOrDefaultAsync(w => w.Id == req.WarehouseId && w.IsActive, ct) ?? throw new NotFoundException("Warehouse");
        if (!(await access.EntitiesWithAsync(Permissions.PoCreate, ct)).Contains(warehouse.EntityId))
            throw new ForbiddenException("You can't order into that warehouse.");
        if (req.Lines.Count == 0) throw new ValidationException("Add at least one line.");
        var (currency, rate) = await ledger.ResolveCurrencyAsync(req.Currency ?? vendor.Currency, req.ExchangeRate, req.Date, ct);

        var itemIds = req.Lines.Select(l => l.ItemId).Distinct().ToList();
        var items = await db.Items.Where(i => itemIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id, ct);
        var taxIds = req.Lines.Where(l => l.TaxRateId != null).Select(l => l.TaxRateId!.Value).ToList();
        var taxes = await db.TaxRates.Where(t => taxIds.Contains(t.Id)).ToDictionaryAsync(t => t.Id, ct);
        var prLineIds = req.Lines.Where(l => l.PurchaseRequestLineId != null).Select(l => l.PurchaseRequestLineId!.Value).ToList();
        var prLines = await db.PurchaseRequestLines.Where(l => prLineIds.Contains(l.Id)).ToDictionaryAsync(l => l.Id, ct);
        var prIds = prLines.Values.Select(l => l.PurchaseRequestId).Distinct().ToList();
        var prs = await db.PurchaseRequests.Where(r => prIds.Contains(r.Id)).ToDictionaryAsync(r => r.Id, ct);

        po.EntityId = req.EntityId;
        po.VendorId = vendor.Id;
        po.WarehouseId = warehouse.Id;
        po.Date = req.Date;
        po.ExpectedDate = req.ExpectedDate;
        po.Currency = currency;
        po.ExchangeRate = rate;
        po.Terms = req.Terms;
        po.Notes = req.Notes;
        var order = 0;
        foreach (var l in req.Lines)
        {
            if (!items.TryGetValue(l.ItemId, out var item) || !item.IsActive) throw new ValidationException("Unknown or inactive item.");
            if (l.Quantity <= 0 || l.UnitPrice < 0) throw new ValidationException("Quantities must be positive and prices not negative.");
            TaxRate? tax = null;
            if (l.TaxRateId is { } tid && !taxes.TryGetValue(tid, out tax)) throw new NotFoundException("Tax rate");
            if (l.PurchaseRequestLineId is { } prl)
            {
                if (!prLines.TryGetValue(prl, out var prLine) || prLine.ItemId != item.Id) throw new ValidationException("Purchase request line doesn't match the item.");
                if (prs[prLine.PurchaseRequestId].Status is not (PurchaseRequestStatus.Approved or PurchaseRequestStatus.Ordered))
                    throw new ValidationException("Only approved purchase requests can be ordered.");
            }
            var amount = LedgerService.Round(l.Quantity * l.UnitPrice);
            po.Lines.Add(new PurchaseOrderLine
            {
                PurchaseOrderId = po.Id, ItemId = item.Id, Description = string.IsNullOrWhiteSpace(l.Description) ? item.Name : l.Description.Trim(),
                Quantity = l.Quantity, UnitPrice = l.UnitPrice, TaxRateId = tax?.Id, Amount = amount,
                TaxAmount = tax == null ? 0 : LedgerService.Round(amount * tax.Rate), PurchaseRequestLineId = l.PurchaseRequestLineId, SortOrder = order++
            });
        }
        po.Subtotal = po.Lines.Sum(l => l.Amount);
        po.TaxTotal = po.Lines.Sum(l => l.TaxAmount);
        po.Total = po.Subtotal + po.TaxTotal;
        await db.SaveChangesAsync(ct);
        return await OrderAsync(po.Id, ct);
    }

    public async Task<PurchaseOrderDto> ApproveOrderAsync(Guid id, CancellationToken ct)
    {
        var po = await db.PurchaseOrders.Include(o => o.Lines).FirstOrDefaultAsync(o => o.Id == id, ct) ?? throw new NotFoundException("Purchase order");
        await access.EnsureAsync(Permissions.PoApprove, po.EntityId, ct);
        if (po.Status != PurchaseOrderStatus.Draft) throw new ValidationException("Only draft orders can be approved.");
        if (po.CreatedBy == currentUser.UserId && !currentUser.IsSuperAdmin) throw new ForbiddenException("A different person must approve the purchase order they raised.");

        // Consume the purchase requests it fulfils.
        var prLineIds = po.Lines.Where(l => l.PurchaseRequestLineId != null).Select(l => l.PurchaseRequestLineId!.Value).ToList();
        var prLines = await db.PurchaseRequestLines.Where(l => prLineIds.Contains(l.Id)).ToListAsync(ct);
        foreach (var l in po.Lines.Where(l => l.PurchaseRequestLineId != null))
            prLines.First(p => p.Id == l.PurchaseRequestLineId).QuantityOrdered += l.Quantity;
        foreach (var prId in prLines.Select(p => p.PurchaseRequestId).Distinct())
        {
            var pr = await db.PurchaseRequests.Include(r => r.Lines).FirstAsync(r => r.Id == prId, ct);
            if (pr.Lines.All(l => l.QuantityOrdered >= l.Quantity)) pr.Status = PurchaseRequestStatus.Ordered;
        }

        po.Status = PurchaseOrderStatus.Approved;
        po.ApprovedBy = currentUser.UserId;
        po.ApprovedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return await OrderAsync(id, ct);
    }

    /// <summary>Cancel an unreceived order, or close a partly received one the vendor won't complete.</summary>
    public async Task<PurchaseOrderDto> CloseOrderAsync(Guid id, CancellationToken ct)
    {
        var po = await db.PurchaseOrders.Include(o => o.Lines).FirstOrDefaultAsync(o => o.Id == id, ct) ?? throw new NotFoundException("Purchase order");
        await access.EnsureAsync(Permissions.PoApprove, po.EntityId, ct);
        if (po.Status is PurchaseOrderStatus.Closed or PurchaseOrderStatus.Cancelled) throw new ValidationException("Already closed.");
        po.Status = po.Lines.Any(l => l.QuantityReceived > 0) ? PurchaseOrderStatus.Closed : PurchaseOrderStatus.Cancelled;
        await db.SaveChangesAsync(ct);
        return await OrderAsync(id, ct);
    }

    // ======================= Goods receipts =======================

    public async Task<GoodsReceiptDto> ReceiveAsync(CreateGoodsReceiptRequest req, CancellationToken ct)
    {
        var po = await db.PurchaseOrders.Include(o => o.Lines).ThenInclude(l => l.Item).Include(o => o.Warehouse).Include(o => o.Vendor)
                     .FirstOrDefaultAsync(o => o.Id == req.PurchaseOrderId, ct) ?? throw new NotFoundException("Purchase order");
        await access.EnsureAsync(Permissions.GrnCreate, po.Warehouse!.EntityId, ct);
        if (po.Status is not (PurchaseOrderStatus.Approved or PurchaseOrderStatus.PartiallyReceived))
            throw new ValidationException("Goods can only be received against an approved, open purchase order.");
        if (req.Date < po.Date) throw new ValidationException("The receipt date is before the order date.");
        await ledger.EnsureOpenPeriodAsync(req.Date, ct);
        var settings = await ledger.SettingsAsync(ct);
        var grni = settings.GrniAccountId ?? throw new ValidationException("Set the 'goods received not invoiced' account in Finance settings.");

        var grn = new GoodsReceipt
        {
            TenantId = currentUser.TenantId!.Value, EntityId = po.Warehouse.EntityId, PurchaseOrderId = po.Id, WarehouseId = po.WarehouseId, Date = req.Date,
            DeliveryNote = req.DeliveryNote, Notes = req.Notes, Number = await ledger.NextNumberAsync("GRN", req.Date, ct)
        };
        var journal = new List<LineInput>();
        foreach (var l in req.Lines.Where(l => l.Quantity != 0))
        {
            var line = po.Lines.FirstOrDefault(x => x.Id == l.PurchaseOrderLineId) ?? throw new ValidationException("Line is not on this purchase order.");
            var open = line.Quantity - line.QuantityReceived;
            if (l.Quantity < 0 || l.Quantity > open)
                throw new ValidationException($"{line.Item!.Code}: receive between 0 and {open:0.####} (ordered {line.Quantity:0.####}, received {line.QuantityReceived:0.####}).");
            var item = line.Item!;
            var unitCost = Math.Round(line.UnitPrice * po.ExchangeRate, 4);
            var value = LedgerService.Round(l.Quantity * line.UnitPrice * po.ExchangeRate);
            var batch = await engine.ResolveBatchAsync(item, l.BatchNo, l.ExpiryDate, ct);
            if (item.Type == ItemType.Stock)
                await engine.InAsync(item, po.WarehouseId, po.Warehouse.EntityId, batch, l.Quantity, unitCost, req.Date, StockMovementType.Receipt, grn.Id, grn.Number, ct, value);

            // Stock → inventory asset; services → their expense account, both accrued until the bill arrives.
            var debit = item.InventoryAccountId ?? (item.Type == ItemType.Stock ? settings.DefaultInventoryAccountId : item.ConsumptionAccountId ?? settings.DefaultConsumptionAccountId)
                        ?? throw new ValidationException("Set the default inventory and consumption accounts.");
            journal.Add(new(debit, value, 0, po.Warehouse.EntityId, $"{item.Code} {item.Name} × {l.Quantity:0.####}"));
            journal.Add(new(grni, 0, value, po.Warehouse.EntityId, $"{po.Number} — {po.Vendor!.Name}", po.VendorId));

            line.QuantityReceived += l.Quantity;
            line.ReceivedBaseValue += value;
            grn.Lines.Add(new GoodsReceiptLine { GoodsReceiptId = grn.Id, PurchaseOrderLineId = line.Id, ItemId = item.Id, Quantity = l.Quantity, BatchId = batch, UnitCost = unitCost, Value = value });
        }
        if (grn.Lines.Count == 0) throw new ValidationException("Enter the quantity received for at least one line.");
        grn.TotalValue = grn.Lines.Sum(l => l.Value);
        po.Status = po.Lines.All(l => l.QuantityReceived >= l.Quantity) ? PurchaseOrderStatus.Received : PurchaseOrderStatus.PartiallyReceived;

        if (journal.Sum(j => j.Debit) > 0)
        {
            var entry = await ledger.BuildAndPostAsync(po.Warehouse.EntityId, req.Date, $"Goods received {grn.Number} against {po.Number}", settings.BaseCurrency, 1,
                JournalSource.GoodsReceipt, grn.Id, journal, po.Number, ct);
            grn.JournalEntryId = entry.Id;
        }
        db.GoodsReceipts.Add(grn);
        await db.SaveChangesAsync(ct);
        return await ReceiptAsync(grn.Id, ct);
    }

    public async Task<List<GoodsReceiptDto>> ReceiptsAsync(Guid? purchaseOrderId, CancellationToken ct)
    {
        var visible = await access.EntitiesWithAsync(Permissions.GrnView, ct);
        visible.UnionWith(await access.EntitiesWithAsync(Permissions.PoView, ct));
        var q = db.GoodsReceipts.Where(g => visible.Contains(g.EntityId));
        if (purchaseOrderId != null) q = q.Where(g => g.PurchaseOrderId == purchaseOrderId);
        var ids = await q.OrderByDescending(g => g.Date).ThenByDescending(g => g.CreatedAt).Take(200).Select(g => g.Id).ToListAsync(ct);
        var list = new List<GoodsReceiptDto>();
        foreach (var id in ids) list.Add(await LoadReceiptAsync(id, ct));
        return list;
    }

    public async Task<GoodsReceiptDto> ReceiptAsync(Guid id, CancellationToken ct)
    {
        var dto = await LoadReceiptAsync(id, ct);
        var entity = await db.GoodsReceipts.Where(g => g.Id == id).Select(g => g.EntityId).FirstAsync(ct);
        if (!await access.HasAsync(Permissions.GrnView, entity, ct)) await access.EnsureAsync(Permissions.PoView, entity, ct);
        return dto;
    }

    private async Task<GoodsReceiptDto> LoadReceiptAsync(Guid id, CancellationToken ct)
    {
        var g = await db.GoodsReceipts.Include(x => x.PurchaseOrder).ThenInclude(p => p!.Vendor).Include(x => x.Warehouse)
                    .Include(x => x.Lines).ThenInclude(l => l.Item).Include(x => x.Lines).ThenInclude(l => l.Batch)
                    .FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Goods receipt");
        var by = g.CreatedBy == null ? null : await db.Users.Where(u => u.Id == g.CreatedBy).Select(u => u.FullName).FirstOrDefaultAsync(ct);
        return new GoodsReceiptDto(g.Id, g.Number, g.PurchaseOrderId, g.PurchaseOrder!.Number, g.PurchaseOrder.Vendor!.Name, g.WarehouseId, g.Warehouse!.Name,
            g.Date, g.DeliveryNote, g.Notes, g.TotalValue, g.JournalEntryId, by,
            g.Lines.Select(l => new GrnLineDto(l.ItemId, l.Item!.Code, l.Item.Name, l.Item.Unit, l.Quantity, l.Batch?.BatchNo, l.Batch?.ExpiryDate, l.UnitCost, l.Value)).ToList());
    }

    // ======================= Bill from PO =======================

    /// <summary>Drafts the vendor bill for everything received but not yet billed, at PO prices. Matched again on approval.</summary>
    public async Task<Guid> CreateBillAsync(Guid purchaseOrderId, CancellationToken ct)
    {
        var po = await db.PurchaseOrders.Include(o => o.Lines).FirstOrDefaultAsync(o => o.Id == purchaseOrderId, ct) ?? throw new NotFoundException("Purchase order");
        await access.EnsureAsync("finance.bills.create", po.EntityId, ct);
        var settings = await ledger.SettingsAsync(ct);
        var grni = settings.GrniAccountId ?? throw new ValidationException("Set the 'goods received not invoiced' account in Finance settings.");
        var pendingDrafts = await db.FinanceDocuments.Where(d => d.PurchaseOrderId == po.Id && d.Status == DocumentStatus.Draft).AnyAsync(ct);
        if (pendingDrafts) throw new ValidationException("A draft bill for this order already exists. Approve or delete it first.");
        var lines = po.Lines.Where(l => l.QuantityReceived > l.QuantityBilled).OrderBy(l => l.SortOrder).ToList();
        if (lines.Count == 0) throw new ValidationException("Nothing has been received that isn't billed yet.");

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var vendor = await db.Contacts.FirstAsync(c => c.Id == po.VendorId, ct);
        var rate = po.Currency == settings.BaseCurrency ? 1 : await ledger.RateAsync(po.Currency, today, ct);
        var bill = new FinanceDocument
        {
            TenantId = po.TenantId, EntityId = po.EntityId, Kind = DocumentKind.Bill, ContactId = po.VendorId, Date = today,
            DueDate = today.AddDays(vendor.PaymentTermsDays), Reference = po.Number, Currency = po.Currency, ExchangeRate = rate, PurchaseOrderId = po.Id,
            Notes = $"Against purchase order {po.Number}"
        };
        var order = 0;
        foreach (var l in lines)
        {
            var qty = l.QuantityReceived - l.QuantityBilled;
            var amount = LedgerService.Round(qty * l.UnitPrice);
            var taxRate = l.TaxRateId == null ? 0 : await db.TaxRates.Where(t => t.Id == l.TaxRateId).Select(t => t.Rate).FirstAsync(ct);
            bill.Lines.Add(new FinanceDocumentLine
            {
                DocumentId = bill.Id, Description = l.Description, AccountId = grni, Quantity = qty, UnitPrice = l.UnitPrice, Amount = amount,
                TaxRateId = l.TaxRateId, TaxAmount = LedgerService.Round(amount * taxRate), PurchaseOrderLineId = l.Id, SortOrder = order++
            });
        }
        bill.Subtotal = bill.Lines.Sum(l => l.Amount);
        bill.TaxTotal = bill.Lines.Sum(l => l.TaxAmount);
        bill.Total = bill.Subtotal + bill.TaxTotal;
        db.FinanceDocuments.Add(bill);
        await db.SaveChangesAsync(ct);
        return bill.Id;
    }

    /// <summary>Received-not-billed per PO line: the detail behind the GRNI account.</summary>
    public async Task<List<GrniRow>> GrniAsync(CancellationToken ct)
    {
        var visible = await access.EntitiesWithAsync(Permissions.PoView, ct);
        return await db.PurchaseOrderLines
            .Join(db.PurchaseOrders, l => l.PurchaseOrderId, o => o.Id, (l, o) => new { l, o })
            .Where(x => visible.Contains(x.o.EntityId) && x.l.ReceivedBaseValue > x.l.BilledBaseValue)
            .OrderBy(x => x.o.Number)
            .Select(x => new GrniRow(x.o.Id, x.o.Number, x.o.Vendor!.Name, x.l.Item!.Code, x.l.Item.Name, x.l.QuantityReceived, x.l.QuantityBilled,
                x.l.ReceivedBaseValue - x.l.BilledBaseValue)).ToListAsync(ct);
    }

    // ---------------- helpers ----------------

    private async Task<Dictionary<Guid, string>> UserNamesAsync(IEnumerable<Guid?> ids, CancellationToken ct)
    {
        var list = ids.Where(i => i != null).Select(i => i!.Value).Distinct().ToList();
        return await db.Users.Where(u => list.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, ct);
    }

    private static string? Name(Dictionary<Guid, string> names, Guid? id) => id is { } v ? names.GetValueOrDefault(v) : null;
}
