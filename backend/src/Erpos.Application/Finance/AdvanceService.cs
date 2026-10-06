using Erpos.Application.Common;
using Erpos.Domain.Entities;
using Erpos.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Erpos.Application.Finance;

/// <summary>
/// Customer advances (deposits) in base currency:
///   receive:  Dr Bank / Cr Customer advances
///   apply:    Dr Customer advances / Cr Receivable  — and the invoice is marked paid by that amount.
/// Callers check their own module permissions and save.
/// </summary>
public class AdvanceService(IAppDbContext db, LedgerService ledger)
{
    private async Task<(FinanceSettings Settings, Guid Advances)> SetupAsync(CancellationToken ct)
    {
        var s = await ledger.SettingsAsync(ct);
        return (s, s.CustomerAdvanceAccountId ?? throw new ValidationException("Set the customer advances account in Finance settings."));
    }

    public async Task<JournalEntry> ReceiveAsync(Guid entityId, Guid contactId, Guid bankAccountId, decimal amount, DateOnly date, string description,
        string? reference, Guid sourceId, CancellationToken ct)
    {
        if (amount <= 0) throw new ValidationException("Deposit amount must be positive.");
        var (settings, advances) = await SetupAsync(ct);
        var bank = await db.Accounts.FirstOrDefaultAsync(a => a.Id == bankAccountId, ct) ?? throw new NotFoundException("Bank account");
        if (bank.SubType is not (AccountSubType.Bank or AccountSubType.Cash)) throw new ValidationException("Choose a bank or cash account.");
        if (bank.Currency != null && bank.Currency != settings.BaseCurrency) throw new ValidationException($"Deposits are taken in {settings.BaseCurrency}.");
        return await ledger.BuildAndPostAsync(entityId, date, description, settings.BaseCurrency, 1, JournalSource.Payment, sourceId,
            [new(bankAccountId, amount, 0, null, description, contactId), new(advances, 0, amount, null, description, contactId)], reference, ct);
    }

    /// <summary>Uses held deposits to pay (part of) an approved base-currency invoice.</summary>
    public async Task<JournalEntry> ApplyAsync(FinanceDocument invoice, decimal amount, DateOnly date, string description, CancellationToken ct)
    {
        var (settings, advances) = await SetupAsync(ct);
        if (invoice.Kind != DocumentKind.Invoice || invoice.Status is not (DocumentStatus.Open or DocumentStatus.PartiallyPaid))
            throw new ValidationException("Advances can only be applied to open invoices.");
        if (invoice.Currency != settings.BaseCurrency) throw new ValidationException("Advances apply to base-currency invoices only.");
        var balance = invoice.Total - invoice.AmountPaid;
        if (amount <= 0 || amount > balance) throw new ValidationException($"Apply between 0 and {balance:N2}.");
        var receivable = settings.ReceivableAccountId ?? throw new ValidationException("Set the receivable account.");

        var entry = await ledger.BuildAndPostAsync(invoice.EntityId, date, description, settings.BaseCurrency, 1, JournalSource.Payment, invoice.Id,
            [new(advances, amount, 0, null, description, invoice.ContactId), new(receivable, 0, amount, null, invoice.Number, invoice.ContactId)], invoice.Number, ct);
        invoice.AmountPaid += amount;
        invoice.BasePaid += amount;
        invoice.Status = invoice.AmountPaid >= invoice.Total ? DocumentStatus.Paid : DocumentStatus.PartiallyPaid;
        return entry;
    }
}
