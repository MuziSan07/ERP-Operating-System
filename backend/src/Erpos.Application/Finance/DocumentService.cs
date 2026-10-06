using Erpos.Application.Authorization;
using Erpos.Application.Common;
using Erpos.Application.Dtos;
using Erpos.Domain.Entities;
using Erpos.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Erpos.Application.Finance;

/// <summary>
/// Sales invoices and purchase bills. Draft → Approve (posts the journal, gets its number) → Open → paid by payments.
/// Invoice: Dr Receivable (total) / Cr Income (net) / Cr Output tax. Bill: Dr Expense / Dr Input tax / Cr Payable.
/// </summary>
/// <summary>
/// Lets a module undo its link to an invoice or bill when Finance voids it (hours back to "approved", consignment back to
/// "unbilled", grant spending reversed…). Handlers run inside the void's transaction; the caller saves.
/// </summary>
public interface IDocumentVoidHandler
{
    Task OnVoidedAsync(FinanceDocument document, CancellationToken ct);
}

public class DocumentService(IAppDbContext db, IAccessService access, ICurrentUser currentUser, LedgerService ledger, IEnumerable<IDocumentVoidHandler> voidHandlers)
{
    private static string Perm(DocumentKind kind, string action) => $"finance.{(kind == DocumentKind.Invoice ? "invoices" : "bills")}.{action}";
    private static string Prefix(DocumentKind kind) => kind == DocumentKind.Invoice ? "INV" : "BILL";
    private static string Label(DocumentKind kind) => kind == DocumentKind.Invoice ? "Invoice" : "Bill";

    public async Task<PagedResult<DocumentListItem>> ListAsync(DocumentKind kind, Guid? entityId, Guid? contactId, DocumentStatus? status,
        bool overdueOnly, string? search, int page, int pageSize, CancellationToken ct)
    {
        var visible = await access.EntitiesWithAsync(Perm(kind, "view"), ct);
        var q = db.FinanceDocuments.Where(d => d.Kind == kind && visible.Contains(d.EntityId));
        if (entityId != null) q = q.Where(d => d.EntityId == entityId);
        if (contactId != null) q = q.Where(d => d.ContactId == contactId);
        if (status != null) q = q.Where(d => d.Status == status);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (overdueOnly) q = q.Where(d => (d.Status == DocumentStatus.Open || d.Status == DocumentStatus.PartiallyPaid) && d.DueDate < today);
        if (!string.IsNullOrWhiteSpace(search)) q = q.Where(d => d.Number.Contains(search) || d.Contact!.Name.Contains(search) || (d.Reference != null && d.Reference.Contains(search)));

        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 200);
        var total = await q.CountAsync(ct);
        var rows = await q.OrderByDescending(d => d.Date).ThenByDescending(d => d.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(d => new { d.Id, d.Kind, d.Number, EntityName = d.Entity!.Name, d.ContactId, ContactName = d.Contact!.Name, d.Date, d.DueDate,
                d.Reference, d.Currency, d.Status, d.Total, d.AmountPaid })
            .ToListAsync(ct);
        var items = rows.Select(d => new DocumentListItem(d.Id, d.Kind, string.IsNullOrEmpty(d.Number) ? null : d.Number, d.EntityName, d.ContactId,
            d.ContactName, d.Date, d.DueDate, d.Reference, d.Currency, d.Status, d.Total, d.Total - d.AmountPaid,
            d.Status is DocumentStatus.Open or DocumentStatus.PartiallyPaid && d.DueDate < today ? today.DayNumber - d.DueDate.DayNumber : 0)).ToList();
        return new PagedResult<DocumentListItem>(items, total, page, pageSize);
    }

    /// <param name="system">True when another module (e.g. hotel checkout) acts with its own permission check.</param>
    public async Task<DocumentDto> GetAsync(Guid id, CancellationToken ct, bool system = false)
    {
        var d = await db.FinanceDocuments.Include(x => x.Entity).Include(x => x.Contact)
                    .Include(x => x.Lines).ThenInclude(l => l.Account).Include(x => x.Lines).ThenInclude(l => l.TaxRate)
                    .FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Document");
        if (!system) await access.EnsureAsync(Perm(d.Kind, "view"), d.EntityId, ct);
        return new DocumentDto(d.Id, d.Kind, string.IsNullOrEmpty(d.Number) ? null : d.Number, d.EntityId, d.Entity!.Name, d.ContactId,
            d.Contact!.Name, d.Contact.Ntn, d.Contact.Strn, string.Join(", ", new[] { d.Contact.Address, d.Contact.City }.Where(x => !string.IsNullOrEmpty(x))),
            d.Date, d.DueDate, d.Reference, d.Notes, d.Currency, d.ExchangeRate, d.Status, d.Subtotal, d.TaxTotal, d.Total, d.AmountPaid,
            d.Total - d.AmountPaid, d.BaseTotal, d.JournalEntryId, d.CreatedAt, d.PurchaseOrderId,
            d.PurchaseOrderId == null ? null : await db.PurchaseOrders.Where(o => o.Id == d.PurchaseOrderId).Select(o => o.Number).FirstOrDefaultAsync(ct),
            d.Lines.OrderBy(l => l.SortOrder).Select(l => new DocumentLineDto(l.Id, l.Description, l.AccountId, $"{l.Account!.Code} {l.Account.Name}",
                l.Quantity, l.UnitPrice, l.Amount, l.TaxRateId, l.TaxRate?.Name, l.TaxRate?.Rate ?? 0, l.TaxAmount, l.PurchaseOrderLineId)).ToList());
    }

    public async Task<DocumentDto> CreateAsync(DocumentKind kind, SaveDocumentRequest req, CancellationToken ct, bool system = false)
    {
        if (!system) await access.EnsureAsync(Perm(kind, "create"), req.EntityId, ct);
        var doc = new FinanceDocument { TenantId = currentUser.TenantId!.Value, Kind = kind };
        await ApplyAsync(doc, req, ct);
        db.FinanceDocuments.Add(doc);
        await db.SaveChangesAsync(ct);
        return await GetAsync(doc.Id, ct, system);
    }

    public async Task<DocumentDto> UpdateAsync(Guid id, SaveDocumentRequest req, CancellationToken ct)
    {
        var doc = await db.FinanceDocuments.Include(d => d.Lines).FirstOrDefaultAsync(d => d.Id == id, ct) ?? throw new NotFoundException("Document");
        await access.EnsureAsync(Perm(doc.Kind, "edit"), doc.EntityId, ct);
        await access.EnsureAsync(Perm(doc.Kind, "edit"), req.EntityId, ct);
        if (doc.Status != DocumentStatus.Draft) throw new ValidationException($"Only draft {Label(doc.Kind).ToLower()}s can be edited.");
        db.FinanceDocumentLines.RemoveRange(doc.Lines);
        doc.Lines.Clear();
        await ApplyAsync(doc, req, ct);
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var doc = await db.FinanceDocuments.Include(d => d.Lines).FirstOrDefaultAsync(d => d.Id == id, ct) ?? throw new NotFoundException("Document");
        await access.EnsureAsync(Perm(doc.Kind, "delete"), doc.EntityId, ct);
        if (doc.Status != DocumentStatus.Draft) throw new ValidationException("Only drafts can be deleted. Void an approved document instead.");
        db.FinanceDocumentLines.RemoveRange(doc.Lines);
        db.FinanceDocuments.Remove(doc);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Numbers the document and posts its journal.</summary>
    public async Task<DocumentDto> ApproveAsync(Guid id, CancellationToken ct, bool system = false)
    {
        var doc = await db.FinanceDocuments.Include(d => d.Lines).ThenInclude(l => l.TaxRate).Include(d => d.Contact)
                      .FirstOrDefaultAsync(d => d.Id == id, ct) ?? throw new NotFoundException("Document");
        if (!system) await access.EnsureAsync(Perm(doc.Kind, "approve"), doc.EntityId, ct);
        if (doc.Status != DocumentStatus.Draft) throw new ValidationException("This document is already approved.");
        var settings = await ledger.SettingsAsync(ct);
        var controlAccount = (doc.Kind == DocumentKind.Invoice ? settings.ReceivableAccountId : settings.PayableAccountId)
                             ?? throw new ValidationException("Set the receivable/payable accounts in Finance settings.");

        var isInvoice = doc.Kind == DocumentKind.Invoice;
        var lines = new List<LineInput>();
        decimal baseSum = 0;
        void Add(Guid account, decimal amount, string? description, Guid? taxRateId)
        {
            if (amount == 0) return;
            var baseAmount = LedgerService.Round(amount * doc.ExchangeRate);
            baseSum += baseAmount;
            // Income/tax credited on invoices, expense/tax debited on bills.
            lines.Add(isInvoice
                ? new LineInput(account, 0, amount, null, description, doc.ContactId, taxRateId, 0, baseAmount)
                : new LineInput(account, amount, 0, null, description, doc.ContactId, taxRateId, baseAmount, 0));
        }
        var poLines = await MatchPurchaseOrderAsync(doc, settings, ct);
        foreach (var l in doc.Lines.OrderBy(l => l.SortOrder))
        {
            if (l.PurchaseOrderLineId is not { } poLineId) { Add(l.AccountId, l.Amount, l.Description, l.TaxRateId); continue; }
            // Three-way matched line: clear the GRN accrual at its recorded value; any price/FX difference is a variance.
            var poLine = poLines[poLineId];
            var available = poLine.QuantityReceived - poLine.QuantityBilled;
            var cleared = l.Quantity == available ? poLine.ReceivedBaseValue - poLine.BilledBaseValue
                : LedgerService.Round(l.Quantity * poLine.ReceivedBaseValue / poLine.QuantityReceived);
            var baseAmount = LedgerService.Round(l.Amount * doc.ExchangeRate);
            baseSum += baseAmount;
            lines.Add(new LineInput(settings.GrniAccountId!.Value, l.Amount, 0, null, l.Description, doc.ContactId, l.TaxRateId, cleared, 0));
            var variance = baseAmount - cleared;
            if (variance != 0)
            {
                var pv = settings.PriceVarianceAccountId ?? throw new ValidationException("Set the purchase price variance account.");
                lines.Add(variance > 0 ? new LineInput(pv, 0, 0, null, "Purchase price / exchange variance", null, null, variance, 0)
                    : new LineInput(pv, 0, 0, null, "Purchase price / exchange variance", null, null, 0, -variance));
            }
            l.MatchedBaseValue = cleared;
            poLine.QuantityBilled += l.Quantity;
            poLine.BilledBaseValue += cleared;
        }
        foreach (var g in doc.Lines.Where(l => l.TaxRate != null && l.TaxAmount != 0).GroupBy(l => l.TaxRate!))
        {
            var taxAccount = (isInvoice ? g.Key.OutputAccountId : g.Key.InputAccountId) ?? throw new ValidationException($"Tax rate {g.Key.Code} has no account.");
            Add(taxAccount, g.Sum(l => l.TaxAmount), g.Key.Name, g.Key.Id);
        }
        // The control line balances in base currency so receivables/payables match exactly what was posted.
        lines.Insert(0, isInvoice
            ? new LineInput(controlAccount, doc.Total, 0, null, doc.Contact!.Name, doc.ContactId, null, baseSum, 0)
            : new LineInput(controlAccount, 0, doc.Total, null, doc.Contact!.Name, doc.ContactId, null, 0, baseSum));

        doc.Number = await ledger.NextNumberAsync(Prefix(doc.Kind), doc.Date, ct);
        var entry = await ledger.BuildAndPostAsync(doc.EntityId, doc.Date, $"{Label(doc.Kind)} {doc.Number} — {doc.Contact!.Name}",
            doc.Currency, doc.ExchangeRate, isInvoice ? JournalSource.Invoice : JournalSource.Bill, doc.Id, lines, doc.Reference ?? doc.Number, ct);
        doc.JournalEntryId = entry.Id;
        doc.BaseTotal = baseSum;
        doc.Status = DocumentStatus.Open;
        await UpdatePurchaseOrderStatusAsync(doc.PurchaseOrderId, ct);
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct, system);
    }

    /// <summary>Cancels an approved, unpaid document by reversing its journal.</summary>
    public async Task<DocumentDto> VoidAsync(Guid id, ReverseRequest req, CancellationToken ct)
    {
        var doc = await db.FinanceDocuments.Include(d => d.Lines).FirstOrDefaultAsync(d => d.Id == id, ct) ?? throw new NotFoundException("Document");
        await access.EnsureAsync(Perm(doc.Kind, "approve"), doc.EntityId, ct);
        if (doc.Status != DocumentStatus.Open || doc.AmountPaid != 0)
            throw new ValidationException("Only approved documents without payments can be voided. Void the payments first.");
        var entry = await db.JournalEntries.FirstAsync(j => j.Id == doc.JournalEntryId, ct);
        await ledger.ReverseAsync(entry, req.Date ?? doc.Date, req.Reason ?? "Voided", ct);
        doc.Status = DocumentStatus.Void;
        // Give the matched quantities back to the purchase order so a corrected bill can be entered.
        var poLineIds = doc.Lines.Where(l => l.PurchaseOrderLineId != null).Select(l => l.PurchaseOrderLineId!.Value).ToList();
        var poLines = await db.PurchaseOrderLines.Where(l => poLineIds.Contains(l.Id)).ToDictionaryAsync(l => l.Id, ct);
        foreach (var l in doc.Lines.Where(l => l.PurchaseOrderLineId != null))
        {
            var poLine = poLines[l.PurchaseOrderLineId!.Value];
            poLine.QuantityBilled -= l.Quantity;
            poLine.BilledBaseValue -= l.MatchedBaseValue;
        }
        await UpdatePurchaseOrderStatusAsync(doc.PurchaseOrderId, ct);
        foreach (var handler in voidHandlers) await handler.OnVoidedAsync(doc, ct);
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    /// <summary>
    /// Three-way match for bills raised from a purchase order: same vendor and currency as the PO, quantity not more than
    /// received-but-unbilled, unit price within the tolerance of the PO price.
    /// </summary>
    private async Task<Dictionary<Guid, PurchaseOrderLine>> MatchPurchaseOrderAsync(FinanceDocument doc, FinanceSettings settings, CancellationToken ct)
    {
        var ids = doc.Lines.Where(l => l.PurchaseOrderLineId != null).Select(l => l.PurchaseOrderLineId!.Value).ToList();
        if (ids.Count == 0) return [];
        var po = await db.PurchaseOrders.FirstAsync(o => o.Id == doc.PurchaseOrderId, ct);
        if (po.VendorId != doc.ContactId) throw new ValidationException($"Three-way match: the bill's vendor differs from purchase order {po.Number}.");
        if (po.Currency != doc.Currency) throw new ValidationException($"Three-way match: {po.Number} is in {po.Currency}; the bill is in {doc.Currency}.");
        if (settings.GrniAccountId == null) throw new ValidationException("Set the 'goods received not invoiced' account in Finance settings.");
        var lines = await db.PurchaseOrderLines.Include(l => l.Item).Where(l => ids.Contains(l.Id)).ToDictionaryAsync(l => l.Id, ct);

        foreach (var g in doc.Lines.Where(l => l.PurchaseOrderLineId != null).GroupBy(l => l.PurchaseOrderLineId!.Value))
        {
            var poLine = lines[g.Key];
            var billing = g.Sum(l => l.Quantity);
            var available = poLine.QuantityReceived - poLine.QuantityBilled;
            if (billing > available)
                throw new ValidationException($"Three-way match: {poLine.Item!.Code} bills {billing:0.####} but only {available:0.####} received and not yet billed.");
            foreach (var l in g)
                if (Math.Abs(l.UnitPrice - poLine.UnitPrice) > poLine.UnitPrice * settings.PriceTolerance)
                    throw new ValidationException($"Three-way match: {poLine.Item!.Code} price {l.UnitPrice:N2} differs from the PO price {poLine.UnitPrice:N2} by more than {settings.PriceTolerance:P0}.");
        }
        return lines;
    }

    private async Task UpdatePurchaseOrderStatusAsync(Guid? purchaseOrderId, CancellationToken ct)
    {
        if (purchaseOrderId == null) return;
        var po = await db.PurchaseOrders.Include(o => o.Lines).FirstAsync(o => o.Id == purchaseOrderId, ct);
        if (po.Status == PurchaseOrderStatus.Cancelled) return;
        var fullyBilled = po.Lines.All(l => l.QuantityBilled >= l.QuantityReceived) && po.Lines.Any(l => l.QuantityReceived > 0);
        if (po.Status == PurchaseOrderStatus.Received && fullyBilled) po.Status = PurchaseOrderStatus.Closed;
        else if (po.Status == PurchaseOrderStatus.Closed && !fullyBilled && po.Lines.All(l => l.QuantityReceived >= l.Quantity)) po.Status = PurchaseOrderStatus.Received;
    }

    private async Task ApplyAsync(FinanceDocument doc, SaveDocumentRequest req, CancellationToken ct)
    {
        var contact = await db.Contacts.FirstOrDefaultAsync(c => c.Id == req.ContactId, ct) ?? throw new NotFoundException("Contact");
        if (doc.Kind == DocumentKind.Invoice && !contact.IsCustomer) throw new ValidationException($"{contact.Name} is not set up as a customer.");
        if (doc.Kind == DocumentKind.Bill && !contact.IsVendor) throw new ValidationException($"{contact.Name} is not set up as a vendor.");
        if (!await db.Entities.AnyAsync(e => e.Id == req.EntityId, ct)) throw new NotFoundException("Entity");
        if (req.Lines.Count == 0) throw new ValidationException("Add at least one line.");
        await ledger.EnsureOpenPeriodAsync(req.Date, ct);

        var (currency, rate) = await ledger.ResolveCurrencyAsync(req.Currency ?? contact.Currency, req.ExchangeRate, req.Date, ct);
        var accountIds = req.Lines.Select(l => l.AccountId).Distinct().ToList();
        var accounts = await db.Accounts.Where(a => accountIds.Contains(a.Id)).ToDictionaryAsync(a => a.Id, ct);
        var taxIds = req.Lines.Where(l => l.TaxRateId != null).Select(l => l.TaxRateId!.Value).Distinct().ToList();
        var taxes = await db.TaxRates.Where(t => taxIds.Contains(t.Id)).ToDictionaryAsync(t => t.Id, ct);

        doc.EntityId = req.EntityId;
        doc.ContactId = contact.Id;
        doc.Date = req.Date;
        doc.DueDate = req.DueDate ?? req.Date.AddDays(contact.PaymentTermsDays);
        if (doc.DueDate < doc.Date) throw new ValidationException("The due date is before the document date.");
        doc.Reference = req.Reference;
        doc.Notes = req.Notes;
        doc.Currency = currency;
        doc.ExchangeRate = rate;

        var order = 0;
        foreach (var l in req.Lines)
        {
            if (!accounts.TryGetValue(l.AccountId, out var acc)) throw new NotFoundException("Account");
            if (acc.IsGroup || !acc.IsActive) throw new ValidationException($"{acc.Code} {acc.Name} can't be used on a line.");
            if (l.Quantity <= 0) throw new ValidationException("Quantity must be positive.");
            if (l.UnitPrice < 0) throw new ValidationException("Unit price can't be negative.");
            TaxRate? tax = null;
            if (l.TaxRateId is { } tid && (!taxes.TryGetValue(tid, out tax) || !tax.IsActive)) throw new ValidationException("Unknown or inactive tax rate.");

            var amount = LedgerService.Round(l.Quantity * l.UnitPrice);
            doc.Lines.Add(new FinanceDocumentLine
            {
                DocumentId = doc.Id, Description = Guard.Required(l.Description, "Line description", 300), AccountId = l.AccountId,
                Quantity = l.Quantity, UnitPrice = l.UnitPrice, Amount = amount, TaxRateId = tax?.Id,
                TaxAmount = tax == null ? 0 : LedgerService.Round(amount * tax.Rate), PurchaseOrderLineId = l.PurchaseOrderLineId, SortOrder = order++
            });
        }
        if (req.Lines.Any(l => l.PurchaseOrderLineId != null))
        {
            if (doc.Kind != DocumentKind.Bill || doc.PurchaseOrderId == null) throw new ValidationException("Only bills created from a purchase order can reference its lines.");
            var ids = req.Lines.Where(l => l.PurchaseOrderLineId != null).Select(l => l.PurchaseOrderLineId!.Value).ToList();
            if (await db.PurchaseOrderLines.CountAsync(l => ids.Contains(l.Id) && l.PurchaseOrderId == doc.PurchaseOrderId, ct) != ids.Distinct().Count())
                throw new ValidationException("A line doesn't belong to this bill's purchase order.");
        }
        doc.Subtotal = doc.Lines.Sum(l => l.Amount);
        doc.TaxTotal = doc.Lines.Sum(l => l.TaxAmount);
        doc.Total = doc.Subtotal + doc.TaxTotal;
        if (doc.Total <= 0) throw new ValidationException("The total must be greater than zero.");
    }
}
