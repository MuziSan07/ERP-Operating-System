using Erpos.Application.Common;
using Erpos.Domain.Entities;
using Erpos.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Erpos.Application.Finance;

/// <summary>One side of a journal line. Base amounts are computed from the rate unless given explicitly.</summary>
public record LineInput(Guid AccountId, decimal Debit, decimal Credit, Guid? EntityId = null, string? Description = null,
    Guid? ContactId = null, Guid? TaxRateId = null, decimal? BaseDebit = null, decimal? BaseCredit = null);

/// <summary>
/// The double-entry engine every module posts through. Posted entries are immutable; corrections are reversals.
/// Each line carries its amount in the entry currency and in the organization's base currency, and both must balance.
/// </summary>
public class LedgerService(IAppDbContext db, ICurrentUser currentUser)
{
    private FinanceSettings? _settings;

    public async Task<FinanceSettings> SettingsAsync(CancellationToken ct)
    {
        if (_settings != null) return _settings;
        _settings = await db.FinanceSettings.FirstOrDefaultAsync(ct);
        if (_settings == null)
        {
            await FinanceDefaults.EnsureAsync(db, currentUser.TenantId!.Value, ct);
            await db.SaveChangesAsync(ct);
            _settings = await db.FinanceSettings.FirstAsync(ct);
        }
        return _settings;
    }

    public static decimal Round(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);

    /// <summary>Base-currency units per unit of <paramref name="currency"/> on a date (latest rate on or before it).</summary>
    public async Task<decimal> RateAsync(string currency, DateOnly date, CancellationToken ct)
    {
        var settings = await SettingsAsync(ct);
        if (string.Equals(currency, settings.BaseCurrency, StringComparison.OrdinalIgnoreCase)) return 1;
        var rate = await db.ExchangeRates.Where(r => r.Currency == currency && r.Date <= date)
            .OrderByDescending(r => r.Date).Select(r => (decimal?)r.Rate).FirstOrDefaultAsync(ct);
        return rate ?? throw new ValidationException($"No {currency} exchange rate on or before {date:dd MMM yyyy}. Add one in Finance settings.");
    }

    /// <summary>Resolves the rate for a document: explicit value, else the stored rate for the date.</summary>
    public async Task<(string Currency, decimal Rate)> ResolveCurrencyAsync(string? currency, decimal? rate, DateOnly date, CancellationToken ct)
    {
        var settings = await SettingsAsync(ct);
        var cur = string.IsNullOrWhiteSpace(currency) ? settings.BaseCurrency : currency.Trim().ToUpperInvariant();
        if (cur.Length != 3) throw new ValidationException("Currency must be a 3-letter ISO code (PKR, USD…).");
        if (cur == settings.BaseCurrency) return (cur, 1);
        if (rate is > 0) return (cur, rate.Value);
        return (cur, await RateAsync(cur, date, ct));
    }

    public async Task EnsureOpenPeriodAsync(DateOnly date, CancellationToken ct)
    {
        var settings = await SettingsAsync(ct);
        if (settings.LockedThrough is { } locked && date <= locked)
            throw new ValidationException($"The books are closed through {locked:dd MMM yyyy}. Use a later date.");
    }

    /// <summary>Next number of a series per fiscal year, e.g. INV-2027-00042 (fiscal year named by its ending year).</summary>
    public async Task<string> NextNumberAsync(string prefix, DateOnly date, CancellationToken ct)
    {
        var settings = await SettingsAsync(ct);
        var fy = settings.FiscalYearStartMonth > 1 && date.Month >= settings.FiscalYearStartMonth ? date.Year + 1 : date.Year;
        var key = $"{prefix}-{fy}";
        return $"{key}-{await db.NextSequenceAsync(key, ct):D5}";
    }

    /// <summary>Builds a balanced entry (not saved). Validates accounts, period, currency and balance.</summary>
    public async Task<JournalEntry> BuildAsync(Guid entityId, DateOnly date, string description, string currency, decimal rate,
        JournalSource source, Guid? sourceId, IReadOnlyList<LineInput> lines, string? reference, CancellationToken ct)
    {
        await EnsureOpenPeriodAsync(date, ct);
        var settings = await SettingsAsync(ct);
        var real = lines.Where(l => l.Debit != 0 || l.Credit != 0 || (l.BaseDebit ?? 0) != 0 || (l.BaseCredit ?? 0) != 0).ToList();
        if (real.Count < 2) throw new ValidationException("A journal needs at least two lines.");
        if (real.Any(l => l.Debit < 0 || l.Credit < 0 || (l.Debit > 0 && l.Credit > 0)))
            throw new ValidationException("Each line is either a debit or a credit, and amounts can't be negative.");

        var accountIds = real.Select(l => l.AccountId).Distinct().ToList();
        var accounts = await db.Accounts.Where(a => accountIds.Contains(a.Id)).ToDictionaryAsync(a => a.Id, ct);
        foreach (var id in accountIds)
        {
            if (!accounts.TryGetValue(id, out var a)) throw new NotFoundException("Account");
            if (a.IsGroup) throw new ValidationException($"{a.Code} {a.Name} is a group account and can't be posted to.");
            if (!a.IsActive) throw new ValidationException($"{a.Code} {a.Name} is inactive.");
            if (a.Currency != null && a.Currency != currency && a.Currency != settings.BaseCurrency)
                throw new ValidationException($"{a.Code} {a.Name} is a {a.Currency} account; this entry is in {currency}.");
        }

        if (Round(real.Sum(l => l.Debit)) != Round(real.Sum(l => l.Credit)))
            throw new ValidationException($"Debits ({real.Sum(l => l.Debit):N2}) and credits ({real.Sum(l => l.Credit):N2}) don't balance.");

        var entry = new JournalEntry
        {
            TenantId = currentUser.TenantId!.Value, EntityId = entityId, Date = date, Description = Guard.Required(description, "Description", 500),
            Reference = reference, Currency = currency, ExchangeRate = rate, Source = source, SourceId = sourceId
        };
        var order = 0;
        foreach (var l in real)
            entry.Lines.Add(new JournalLine
            {
                JournalEntryId = entry.Id, AccountId = l.AccountId, EntityId = l.EntityId ?? entityId, Description = l.Description,
                Debit = Round(l.Debit), Credit = Round(l.Credit),
                BaseDebit = l.BaseDebit ?? Round(l.Debit * rate), BaseCredit = l.BaseCredit ?? Round(l.Credit * rate),
                ContactId = l.ContactId, TaxRateId = l.TaxRateId, SortOrder = order++
            });

        // Rounding each line to paisa can leave a tiny base difference; absorb it in the largest line on the short side.
        var diff = entry.Lines.Sum(l => l.BaseDebit) - entry.Lines.Sum(l => l.BaseCredit);
        if (diff != 0)
        {
            if (Math.Abs(diff) > 0.01m * entry.Lines.Count) throw new ValidationException("Base currency amounts don't balance.");
            if (diff > 0) entry.Lines.Where(l => l.Credit > 0 || l.BaseCredit > 0).MaxBy(l => l.BaseCredit)!.BaseCredit += diff;
            else entry.Lines.Where(l => l.Debit > 0 || l.BaseDebit > 0).MaxBy(l => l.BaseDebit)!.BaseDebit -= diff;
        }
        return entry;
    }

    /// <summary>Numbers and posts an entry that is tracked or about to be added. Caller saves.</summary>
    public async Task PostAsync(JournalEntry entry, CancellationToken ct)
    {
        if (entry.Status != JournalStatus.Draft) throw new ValidationException("Only draft entries can be posted.");
        await EnsureOpenPeriodAsync(entry.Date, ct);
        entry.Number = await NextNumberAsync("JV", entry.Date, ct);
        entry.Status = JournalStatus.Posted;
        entry.PostedBy = currentUser.UserId;
        entry.PostedAt = DateTime.UtcNow;
    }

    /// <summary>Builds and posts in one go (generated entries: invoices, payments, payroll). Caller saves.</summary>
    public async Task<JournalEntry> BuildAndPostAsync(Guid entityId, DateOnly date, string description, string currency, decimal rate,
        JournalSource source, Guid? sourceId, IReadOnlyList<LineInput> lines, string? reference, CancellationToken ct)
    {
        var entry = await BuildAsync(entityId, date, description, currency, rate, source, sourceId, lines, reference, ct);
        entry.Number = await NextNumberAsync("JV", date, ct);
        entry.Status = JournalStatus.Posted;
        entry.PostedBy = currentUser.UserId;
        entry.PostedAt = DateTime.UtcNow;
        db.JournalEntries.Add(entry);
        return entry;
    }

    /// <summary>Posts a mirror entry and marks the original as reversed. Caller saves.</summary>
    public async Task<JournalEntry> ReverseAsync(JournalEntry original, DateOnly date, string? reason, CancellationToken ct)
    {
        if (original.Status != JournalStatus.Posted) throw new ValidationException("Only posted entries can be reversed.");
        var lines = await db.JournalLines.Where(l => l.JournalEntryId == original.Id).OrderBy(l => l.SortOrder).ToListAsync(ct);
        var reversal = await BuildAndPostAsync(original.EntityId, date, $"Reversal of {original.Number}{(reason == null ? "" : $": {reason}")}",
            original.Currency, original.ExchangeRate, JournalSource.Reversal, original.Id,
            lines.Select(l => new LineInput(l.AccountId, l.Credit, l.Debit, l.EntityId, l.Description, l.ContactId, l.TaxRateId,
                l.BaseCredit, l.BaseDebit)).ToList(), original.Number, ct);
        reversal.ReversalOfId = original.Id;
        original.Status = JournalStatus.Reversed;
        original.ReversedById = reversal.Id;
        return reversal;
    }
}
