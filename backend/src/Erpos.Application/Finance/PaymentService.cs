using Erpos.Application.Authorization;
using Erpos.Application.Common;
using Erpos.Application.Dtos;
using Erpos.Domain.Entities;
using Erpos.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Erpos.Application.Finance;

/// <summary>
/// Customer receipts and vendor payments, fully allocated to open invoices/bills.
/// Receivables/payables are cleared at each document's own rate; the bank moves at the payment's rate,
/// and any difference is the realized exchange gain or loss.
/// </summary>
public class PaymentService(IAppDbContext db, IAccessService access, ICurrentUser currentUser, LedgerService ledger)
{
    public async Task<PagedResult<PaymentDto>> ListAsync(PaymentKind? kind, Guid? contactId, DateOnly? from, DateOnly? to, int page, int pageSize, CancellationToken ct)
    {
        var visible = await access.EntitiesWithAsync(Permissions.PaymentsView, ct);
        var q = db.Payments.Where(p => visible.Contains(p.EntityId));
        if (kind != null) q = q.Where(p => p.Kind == kind);
        if (contactId != null) q = q.Where(p => p.ContactId == contactId);
        if (from != null) q = q.Where(p => p.Date >= from);
        if (to != null) q = q.Where(p => p.Date <= to);
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 200);
        var total = await q.CountAsync(ct);
        var ids = await q.OrderByDescending(p => p.Date).ThenByDescending(p => p.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).Select(p => p.Id).ToListAsync(ct);
        var items = await LoadAsync(ids, ct);
        return new PagedResult<PaymentDto>(ids.Select(id => items.First(i => i.Id == id)).ToList(), total, page, pageSize);
    }

    public async Task<PaymentDto> GetAsync(Guid id, CancellationToken ct, bool system = false)
    {
        var p = (await LoadAsync([id], ct)).FirstOrDefault() ?? throw new NotFoundException("Payment");
        if (!system) await access.EnsureAsync(Permissions.PaymentsView, p.EntityId, ct);
        return p;
    }

    /// <param name="system">True when another module (e.g. hotel checkout) acts with its own permission check.</param>
    public async Task<PaymentDto> CreateAsync(CreatePaymentRequest req, CancellationToken ct, bool system = false)
    {
        if (!system) await access.EnsureAsync(Permissions.PaymentsCreate, req.EntityId, ct);
        var settings = await ledger.SettingsAsync(ct);
        var receipt = req.Kind == PaymentKind.Receipt;
        var docKind = receipt ? DocumentKind.Invoice : DocumentKind.Bill;

        var contact = await db.Contacts.FirstOrDefaultAsync(c => c.Id == req.ContactId, ct) ?? throw new NotFoundException("Contact");
        var bank = await db.Accounts.FirstOrDefaultAsync(a => a.Id == req.BankAccountId, ct) ?? throw new NotFoundException("Bank account");
        if (bank.SubType is not (AccountSubType.Bank or AccountSubType.Cash) || bank.IsGroup)
            throw new ValidationException("Choose a bank or cash account.");
        var bankCurrency = bank.Currency ?? settings.BaseCurrency;
        var (currency, rate) = await ledger.ResolveCurrencyAsync(req.Currency ?? bankCurrency, req.ExchangeRate, req.Date, ct);
        if (currency != bankCurrency) throw new ValidationException($"{bank.Name} holds {bankCurrency}; record the payment in {bankCurrency}.");
        if (req.Amount <= 0) throw new ValidationException("Amount must be positive.");
        if (req.Allocations.Count == 0) throw new ValidationException($"Allocate the payment to at least one {(receipt ? "invoice" : "bill")}.");
        if (LedgerService.Round(req.Allocations.Sum(a => a.Amount)) != LedgerService.Round(req.Amount))
            throw new ValidationException("Allocations must add up to the payment amount.");

        var docIds = req.Allocations.Select(a => a.DocumentId).ToList();
        if (docIds.Distinct().Count() != docIds.Count) throw new ValidationException("A document appears twice.");
        var docs = await db.FinanceDocuments.Where(d => docIds.Contains(d.Id)).ToDictionaryAsync(d => d.Id, ct);
        var payment = new Payment
        {
            TenantId = currentUser.TenantId!.Value, EntityId = req.EntityId, Kind = req.Kind, ContactId = contact.Id, Date = req.Date,
            BankAccountId = bank.Id, Currency = currency, ExchangeRate = rate, Amount = LedgerService.Round(req.Amount),
            Reference = req.Reference, Notes = req.Notes
        };

        var control = (receipt ? settings.ReceivableAccountId : settings.PayableAccountId) ?? throw new ValidationException("Receivable/payable accounts are not set.");
        var lines = new List<LineInput>();
        decimal clearedBase = 0;
        foreach (var a in req.Allocations)
        {
            if (!docs.TryGetValue(a.DocumentId, out var d)) throw new NotFoundException("Document");
            if (d.Kind != docKind || d.ContactId != contact.Id) throw new ValidationException($"{d.Number} doesn't belong to {contact.Name}.");
            if (d.Status is not (DocumentStatus.Open or DocumentStatus.PartiallyPaid)) throw new ValidationException($"{d.Number} is not open.");
            if (d.Currency != currency) throw new ValidationException($"{d.Number} is in {d.Currency}; this payment is in {currency}.");
            var balance = d.Total - d.AmountPaid;
            if (a.Amount <= 0 || a.Amount > balance) throw new ValidationException($"{d.Number}: allocate between 0 and {balance:N2}.");

            // Clear the receivable/payable at the document's rate; the final payment clears the exact remainder.
            var baseAmount = a.Amount == balance ? d.BaseTotal - d.BasePaid : LedgerService.Round(a.Amount * d.ExchangeRate);
            clearedBase += baseAmount;
            payment.Allocations.Add(new PaymentAllocation { PaymentId = payment.Id, DocumentId = d.Id, Amount = a.Amount, BaseAmount = baseAmount });
            lines.Add(receipt
                ? new LineInput(control, 0, a.Amount, null, d.Number, contact.Id, null, 0, baseAmount)
                : new LineInput(control, a.Amount, 0, null, d.Number, contact.Id, null, baseAmount, 0));

            d.AmountPaid += a.Amount;
            d.BasePaid += baseAmount;
            d.Status = d.AmountPaid >= d.Total ? DocumentStatus.Paid : DocumentStatus.PartiallyPaid;
        }

        var bankBase = LedgerService.Round(payment.Amount * rate);
        lines.Insert(0, receipt
            ? new LineInput(bank.Id, payment.Amount, 0, null, contact.Name, contact.Id, null, bankBase, 0)
            : new LineInput(bank.Id, 0, payment.Amount, null, contact.Name, contact.Id, null, 0, bankBase));

        // Receipt: more base received than cleared = gain. Payment: more base paid than cleared = loss.
        var fx = receipt ? bankBase - clearedBase : clearedBase - bankBase;
        if (fx != 0)
        {
            var fxAccount = settings.ExchangeGainLossAccountId ?? throw new ValidationException("Set the exchange gain/loss account in Finance settings.");
            lines.Add(fx > 0
                ? new LineInput(fxAccount, 0, 0, null, "Realized exchange gain", null, null, 0, fx)
                : new LineInput(fxAccount, 0, 0, null, "Realized exchange loss", null, null, -fx, 0));
        }

        payment.Number = await ledger.NextNumberAsync(receipt ? "RCT" : "PAY", req.Date, ct);
        var entry = await ledger.BuildAndPostAsync(req.EntityId, req.Date, $"{(receipt ? "Receipt from" : "Payment to")} {contact.Name}",
            currency, rate, JournalSource.Payment, payment.Id, lines, req.Reference ?? payment.Number, ct);
        payment.JournalEntryId = entry.Id;
        db.Payments.Add(payment);
        await db.SaveChangesAsync(ct);
        return await GetAsync(payment.Id, ct, system);
    }

    public async Task<PaymentDto> VoidAsync(Guid id, ReverseRequest req, CancellationToken ct)
    {
        var p = await db.Payments.Include(x => x.Allocations).FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Payment");
        await access.EnsureAsync(Permissions.PaymentsApprove, p.EntityId, ct);
        if (p.IsVoid) throw new ValidationException("Already void.");
        var entry = await db.JournalEntries.FirstAsync(j => j.Id == p.JournalEntryId, ct);
        await ledger.ReverseAsync(entry, req.Date ?? p.Date, req.Reason ?? "Payment voided", ct);

        var docIds = p.Allocations.Select(a => a.DocumentId).ToList();
        var docs = await db.FinanceDocuments.Where(d => docIds.Contains(d.Id)).ToDictionaryAsync(d => d.Id, ct);
        foreach (var a in p.Allocations)
        {
            var d = docs[a.DocumentId];
            d.AmountPaid -= a.Amount;
            d.BasePaid -= a.BaseAmount;
            d.Status = d.AmountPaid <= 0 ? DocumentStatus.Open : DocumentStatus.PartiallyPaid;
        }
        p.IsVoid = true;
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    private async Task<List<PaymentDto>> LoadAsync(List<Guid> ids, CancellationToken ct)
    {
        var rows = await db.Payments.Where(p => ids.Contains(p.Id)).Include(p => p.Entity).Include(p => p.Contact).Include(p => p.BankAccount)
            .Include(p => p.Allocations).ThenInclude(a => a.Document).ToListAsync(ct);
        return rows.Select(p => new PaymentDto(p.Id, p.Kind, p.Number, p.EntityId, p.Entity!.Name, p.ContactId, p.Contact!.Name, p.Date,
            p.BankAccountId, $"{p.BankAccount!.Code} {p.BankAccount.Name}", p.Currency, p.ExchangeRate, p.Amount, p.Reference, p.Notes, p.IsVoid,
            p.JournalEntryId, p.Allocations.Select(a => new AllocationDto(a.DocumentId, a.Document?.Number, a.Amount, a.BaseAmount)).ToList())).ToList();
    }
}
